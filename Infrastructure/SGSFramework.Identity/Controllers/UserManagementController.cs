#nullable enable
namespace SGSFramework.Identity.Controllers.v1;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Models;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Logings;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Abstractions.Transactions;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Identity.DTOs;

/// <summary>
/// 使用者帳號管理控制器
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/users")]
[Produces(MediaTypeNames.Application.Json)]
[Consumes(MediaTypeNames.Application.Json)]
[ControllerTitle("使用者管理", Icon = "fa-solid fa-user-gear", Order = 10, Description = "提供使用者註冊、身分驗證、2FA、密碼安全維護與工作階段管理服務")]
[RequiresPermission("SYSTEM.USERMANAGEMENT.READ")]
public sealed class UserManagementController : ApiControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly TokenBucketEngine<ApplicationUser> _tokenEngine;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserManagementController> _logger;
    private readonly ISecurityLogger _securityLogger;

    public UserManagementController(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        SignInManager<ApplicationUser> signInManager,
        TokenBucketEngine<ApplicationUser> tokenEngine,
        IUnitOfWork unitOfWork,
        ILogger<UserManagementController> logger,
        ISecurityLogger securityLogger)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
        _tokenEngine = tokenEngine ?? throw new ArgumentNullException(nameof(tokenEngine));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _securityLogger = securityLogger ?? throw new ArgumentNullException(nameof(securityLogger));
    }

    /// <summary>
    /// 使用者註冊 (支援自訂帳號與 Email 雙重唯一性校驗)
    /// </summary>
    [HttpPost("register")]
    [Function("Register", "使用者註冊", Icon = "fa-solid fa-user-plus", Order = 1, Description = "進行新使用者帳號註冊並生成電子郵件驗證憑證")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.REGISTER")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        try
        {
            var userByName = await _userManager.FindByNameAsync(request.Username);
            if (userByName != null)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "帳號無效",
                    Detail = "該帳號名稱已被使用。",
                    Instance = HttpContext.Request.Path
                });
            }

            var userByEmail = await _userManager.FindByEmailAsync(request.Email);
            if (userByEmail != null)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Email 無效",
                    Detail = "該電子郵件已被註冊。",
                    Instance = HttpContext.Request.Path
                });
            }

            var user = new ApplicationUser
            {
                UserName = request.Username,
                Email = request.Email,
                EmailConfirmed = false,
                LockoutEnabled = true
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "註冊失敗",
                    Detail = string.Join("; ", result.Errors.Select(e => e.Description)),
                    Instance = HttpContext.Request.Path
                });
            }

            string emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);

            _securityLogger.LogSecurity(
                eventCode: "SEC-200-USER-REGISTER",
                eventCategory: "UserManagement.Register",
                userId: user.Id.ToString(),
                clientIp: clientIp,
                messageTemplate: "使用者註冊成功。帳號: {Username}, Email: {Email}",
                user.UserName ?? string.Empty,
                user.Email ?? string.Empty
            );

            return Ok(new { message = "註冊成功，請至電子郵件信箱查收驗證信。", debugToken = emailToken });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "註冊端點發生未預期異常。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "註冊作業處理期間發生未預期錯誤，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 驗證使用者電子郵件
    /// </summary>
    [HttpGet("confirm-email")]
    [Function("ConfirmEmail", "使用者 Email 驗證", Icon = "fa-solid fa-envelope-circle-check", Order = 2, Description = "檢驗使用者電子郵件驗證碼並啟用帳號")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.CONFIRMEMAIL")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string email, [FromQuery] string token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "參數缺失",
                Detail = "無效或未提供必要的驗證參數。",
                Instance = HttpContext.Request.Path
            });
        }

        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        try
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "使用者不存在",
                    Detail = "找不到該電子郵件對應的使用者。",
                    Instance = HttpContext.Request.Path
                });
            }

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded)
            {
                _securityLogger.LogSecurity(
                    eventCode: "SEC-400-EMAIL-CONFIRM-FAILED",
                    eventCategory: "UserManagement.ConfirmEmail",
                    userId: user.Id.ToString(),
                    clientIp: clientIp,
                    messageTemplate: "使用者 Email 驗證失敗。Email: {Email}",
                    email
                );

                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "驗證失敗",
                    Detail = "電子郵件驗證失敗或記號已過期。",
                    Instance = HttpContext.Request.Path
                });
            }

            _securityLogger.LogSecurity(
                eventCode: "SEC-200-EMAIL-CONFIRMED",
                eventCategory: "UserManagement.ConfirmEmail",
                userId: user.Id.ToString(),
                clientIp: clientIp,
                messageTemplate: "使用者 Email 驗證成功，帳號已啟用。Email: {Email}",
                email
            );

            return Ok(new { message = "電子郵件驗證成功！帳號已正式啟用。" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "驗證電子郵件端點發生未預期異常。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "驗證電子郵件時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 驗證雙因子登入 (2FA)
    /// </summary>
    [HttpPost("verify-2fa")]
    [Function("VerifyTwoFactor", "雙因子驗證登入", Icon = "fa-solid fa-key", Order = 4, Description = "驗證使用者雙因子驗證碼 (2FA) 並簽發正式工作階段憑證")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.VERIFYTWOFACTOR")]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] TwoFactorVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "UNKNOWN-DEVICE";
        string deviceName = Request.Headers["User-Agent"].FirstOrDefault() ?? "Generic Browser";
        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        try
        {
            var user = await _userManager.FindByEmailAsync(request.Email) ?? await _userManager.FindByNameAsync(request.Email);
            if (user == null)
            {
                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "認證失敗",
                    Detail = "找不到對應的使用者資訊。",
                    Instance = HttpContext.Request.Path
                });
            }

            var isValid = await _userManager.VerifyTwoFactorTokenAsync(user, "Email", request.Code);
            if (!isValid)
            {
                await _userManager.AccessFailedAsync(user);

                _securityLogger.LogSecurity(
                    eventCode: "SEC-401-2FA-FAILED",
                    eventCategory: "UserManagement.Verify2FA",
                    userId: user.Id.ToString(),
                    clientIp: clientIp,
                    messageTemplate: "雙因子驗證失敗。帳號: {Email}, 裝置: {DeviceId}",
                    user.Email ?? string.Empty,
                    deviceId
                );

                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "驗證碼無效",
                    Detail = "雙因子驗證碼不正確或已逾期。",
                    Instance = HttpContext.Request.Path
                });
            }

            await _userManager.ResetAccessFailedCountAsync(user);

            var tokenResult = await _tokenEngine.IssueInitialSessionAsync(user, deviceId, deviceName, clientIp);

            _securityLogger.LogSecurity(
                eventCode: "SEC-200-2FA-SUCCESS",
                eventCategory: "UserManagement.Verify2FA",
                userId: user.Id.ToString(),
                clientIp: clientIp,
                messageTemplate: "雙因子認證成功並簽發憑證。帳號: {Email}, 裝置: {DeviceId}",
                user.Email ?? string.Empty,
                deviceId
            );

            return Ok(new { message = "雙因子認證成功", tokenData = tokenResult });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "2FA 驗證端點發生未預期異常。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "二次驗證處理期間發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 忘記密碼 - 申請重設權限 (產生加密重設記號)
    /// </summary>
    [HttpPost("forgot-password")]
    [Function("ForgotPassword", "忘記密碼", Icon = "fa-solid fa-unlock-keyhole", Order = 5, Description = "發送密碼重設郵件與記號至使用者信箱")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        try
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                return Ok(new { message = "若帳號存在且已完成啟用，重設密碼信件已發送至您的信箱。" });
            }

            string resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);

            _securityLogger.LogSecurity(
                eventCode: "SEC-200-FORGOT-PASSWORD-REQUEST",
                eventCategory: "UserManagement.ForgotPassword",
                userId: user.Id.ToString(),
                clientIp: clientIp,
                messageTemplate: "使用者申請密碼重設憑證。Email: {Email}",
                request.Email
            );

            return Ok(new { message = "重設密碼信件已發送。", debugResetToken = resetToken });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "忘記密碼端點發生未預期異常。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "發送重設密碼請求時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 重設密碼執行 (忘記密碼情境，使用 Token 換新密碼)
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [Function("ResetPassword", "重設密碼", Icon = "fa-solid fa-shield-cat", Order = 6, Description = "使用重設記號重置密碼，並強制作廢全網歷史 Session 憑證")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        try
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "重設失敗",
                    Detail = "密碼重設失敗，找不到對應的使用者。",
                    Instance = HttpContext.Request.Path
                });
            }

            var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
            if (!result.Succeeded)
            {
                _securityLogger.LogSecurity(
                    eventCode: "SEC-400-PASSWORD-RESET-FAILED",
                    eventCategory: "UserManagement.ResetPassword",
                    userId: user.Id.ToString(),
                    clientIp: clientIp,
                    messageTemplate: "使用者密碼重設失敗。Email: {Email}",
                    request.Email
                );

                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "重設失敗",
                    Detail = "密碼重設失敗，可能記號已失效或新密碼不符合複雜度規範。",
                    Instance = HttpContext.Request.Path
                });
            }

            await _tokenEngine.EmergencyFreezeAsync(user.Id.ToString(), "使用者透過忘記密碼功能完成密碼重設，全面肅清舊有憑證軌跡。");
            await _tokenEngine.CompleteRemediationAsync(user.Id.ToString());

            _securityLogger.LogSecurity(
                eventCode: "SEC-200-PASSWORD-RESET-SUCCESS",
                eventCategory: "UserManagement.ResetPassword",
                userId: user.Id.ToString(),
                clientIp: clientIp,
                messageTemplate: "使用者透過 Token 重設密碼成功，並已強制作廢全網 Session。Email: {Email}",
                request.Email
            );

            return Ok(new { message = "密碼重設成功，已強制終止其餘裝置連線，請使用新密碼重新登入。" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "密碼重設端點發生未預期異常。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "執行密碼重設時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 變更密碼 (登入狀態情境：驗證舊密碼並換新密碼，強制執行登出聯防)
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    [Function("ChangePassword", "變更密碼", Icon = "fa-solid fa-lock-rotate", Order = 7, Description = "使用者登入狀態下變更密碼，並觸發資安聯防註銷其他裝置 Session")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        try
        {
            string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "未授權",
                    Detail = "無法識別目前的登入身分。",
                    Instance = HttpContext.Request.Path
                });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "使用者不存在",
                    Detail = "無法找到該登入使用者的詳細資料。",
                    Instance = HttpContext.Request.Path
                });
            }

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
            if (!result.Succeeded)
            {
                _securityLogger.LogSecurity(
                    eventCode: "SEC-400-PASSWORD-CHANGE-FAILED",
                    eventCategory: "UserManagement.ChangePassword",
                    userId: userId,
                    clientIp: clientIp,
                    messageTemplate: "使用者線上變更密碼失敗。用戶識別碼: {UserId}",
                    userId
                );

                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "變更密碼失敗",
                    Detail = "變更密碼失敗，請確認舊密碼是否輸入正確或符合密碼複雜度原則。",
                    Instance = HttpContext.Request.Path
                });
            }

            await _tokenEngine.EmergencyFreezeAsync(user.Id.ToString(), "使用者執行線上變更密碼，強制登出全網所有裝置工作階段。");
            await _tokenEngine.CompleteRemediationAsync(user.Id.ToString());

            _securityLogger.LogSecurity(
                eventCode: "SEC-200-PASSWORD-CHANGED",
                eventCategory: "UserManagement.ChangePassword",
                userId: userId,
                clientIp: clientIp,
                messageTemplate: "使用者線上變更密碼成功，並已肅清全網 Session。帳號: {Username}",
                user.UserName ?? string.Empty
            );

            return Ok(new { message = "密碼變更成功，其餘裝置連線已被安全強制切斷，請使用新密碼重新登入。" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "變更密碼端點發生未預期核心異常。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "變更密碼時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 安全登出 (註銷當前裝置之活動軌跡)
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    [Function("Logout", "安全登出", Icon = "fa-solid fa-right-from-bracket", Order = 8, Description = "安全登出系統並註銷當前裝置之活動工作階段票據")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Logout()
    {
        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        try
        {
            string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "UNKNOWN-DEVICE";

            if (!string.IsNullOrEmpty(userId))
            {
                await _tokenEngine.EmergencyFreezeAsync(userId, $"用戶主動執行安全登出。裝置識別: {deviceId}");
                await _tokenEngine.CompleteRemediationAsync(userId);

                _securityLogger.LogSecurity(
                    eventCode: "SEC-200-LOGOUT",
                    eventCategory: "UserManagement.Logout",
                    userId: userId,
                    clientIp: clientIp,
                    messageTemplate: "使用者安全登出成功。用戶識別碼: {UserId}, 裝置: {DeviceId}",
                    userId,
                    deviceId
                );
            }

            return Ok(new { message = "已安全登出並註銷工作階段票據。" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "安全登出端點發生未預期異常。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "執行登出作業時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 取得系統所有使用者清單（含所屬角色，自動過濾已軟刪除項目）
    /// </summary>
    [HttpGet]
    [Function("GetUsers", "查詢使用者列表", Icon = "fa-solid fa-users", Order = 9, Description = "取得系統所有有效使用者清單，包含帳號、Email、驗證狀態與所屬角色等資訊", IsMenu = true)]
    [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.GETUSERS")]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var users = await _userManager.Users
                .AsNoTracking()
                .Where(u => !u.IsDeleted)
                .ToListAsync(cancellationToken);

            var userDtos = new List<UserDto>(users.Count);

            foreach (var user in users)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var roles = await _userManager.GetRolesAsync(user);

                userDtos.Add(new UserDto
                {
                    Id = user.Id.ToString(),
                    Username = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    EmailConfirmed = user.EmailConfirmed,
                    LockoutEnabled = user.LockoutEnabled,
                    Roles = roles.ToList()
                });
            }

            return Ok(userDtos);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[UserManagementController] 查詢使用者列表作業已被用戶端取消 (Client Closed Request)。");
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserManagementController] 查詢使用者列表時發生系統異常。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "查詢使用者列表時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 取得指定使用者的完整角色指派狀態 (包含未繫結角色)
    /// </summary>
    [HttpGet("{userId:guid}/roles")]
    [Function("GetUserRoleAssignment", "查詢使用者角色設定", Icon = "fa-solid fa-user-tag", Order = 10, Description = "取得特定使用者包含已指派與未指派的全系統角色狀態")]
    [ProducesResponseType(typeof(UserRoleAssignmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.READ")]
    public async Task<IActionResult> GetUserRoleAssignment(
        [FromRoute] Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null || user.IsDeleted)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "查無使用者",
                    Detail = $"找不到識別碼為 '{userId}' 的有效使用者。",
                    Instance = HttpContext.Request.Path
                });
            }

            var allRoles = await _roleManager.Roles.AsNoTracking().ToListAsync(cancellationToken);
            var userRoleNames = await _userManager.GetRolesAsync(user);
            var assignedSet = new HashSet<string>(userRoleNames, StringComparer.OrdinalIgnoreCase);

            var roleSelectionItems = allRoles.Select(r => new RoleSelectionItemDto
            {
                RoleId = r.Id.ToString(),
                RoleName = r.Name ?? string.Empty,
                Description = r.Name,
                IsAssigned = r.Name != null && assignedSet.Contains(r.Name)
            }).ToList();

            var result = new UserRoleAssignmentDto
            {
                UserId = user.Id.ToString(),
                Username = user.UserName ?? string.Empty,
                Roles = roleSelectionItems
            };

            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[UserManagementController] 查詢使用者角色狀態作業已被用戶端取消。UserId: {UserId}", userId);
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserManagementController] 查詢使用者角色狀態時發生異常。UserId: {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "讀取使用者角色設定時發生系統異常。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 指派/更新指定使用者的角色權限清單
    /// </summary>
    [HttpPut("{userId:guid}/roles")]
    [Function("AssignUserRoles", "指派使用者角色", Icon = "fa-solid fa-user-shield", Order = 11, Description = "更新指定使用者的系統角色權限對應清單")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.ASSIGNUSERROLES")]
    public async Task<IActionResult> AssignUserRoles(
        [FromRoute] Guid userId,
        [FromBody] AssignUserRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "SYSTEM";
        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null || user.IsDeleted)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "使用者不存在",
                    Detail = $"找不到識別碼為 '{userId}' 的有效使用者。",
                    Instance = HttpContext.Request.Path
                });
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var targetRoles = request.RoleNames?.Distinct().ToList() ?? new List<string>();

            var rolesToRemove = currentRoles.Except(targetRoles).ToList();
            var rolesToAdd = targetRoles.Except(currentRoles).ToList();

            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                if (rolesToRemove.Count > 0)
                {
                    var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                    if (!removeResult.Succeeded)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return BadRequest(new ProblemDetails
                        {
                            Status = StatusCodes.Status400BadRequest,
                            Title = "角色指派失敗",
                            Detail = string.Join("; ", removeResult.Errors.Select(e => e.Description)),
                            Instance = HttpContext.Request.Path
                        });
                    }
                }

                if (rolesToAdd.Count > 0)
                {
                    var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
                    if (!addResult.Succeeded)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return BadRequest(new ProblemDetails
                        {
                            Status = StatusCodes.Status400BadRequest,
                            Title = "角色指派失敗",
                            Detail = string.Join("; ", addResult.Errors.Select(e => e.Description)),
                            Instance = HttpContext.Request.Path
                        });
                    }
                }

                await transaction.CommitAsync(cancellationToken);

                _securityLogger.LogSecurity(
                    eventCode: "SEC-200-ROLE-ASSIGNMENT-UPDATED",
                    eventCategory: "UserManagement.AssignRoles",
                    userId: currentUserId,
                    clientIp: clientIp,
                    messageTemplate: "管理員 [{AdminId}] 更新使用者 [{TargetUserId}] 角色清單: [{Roles}]",
                    currentUserId,
                    userId.ToString(),
                    string.Join(", ", targetRoles)
                );

                return Ok(new { message = "使用者角色權限指派成功。" });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[UserManagementController] 指派角色作業已被用戶端取消。UserId: {UserId}", userId);
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新使用者角色時發生未預期異常。UserId: {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "更新使用者角色時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 刪除指定使用者帳號 (採用軟刪除、PII 匿名化與 Token 全網肅清)
    /// </summary>
    [HttpDelete("{userId:guid}")]
    [Function("DeleteUser", "刪除使用者帳號", Icon = "fa-solid fa-user-minus", Order = 12, Description = "軟刪除指定使用者帳號、抹除敏感個資並強制肅清其全網活動 Session")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.DELETEUSER")]
    public async Task<IActionResult> DeleteUser([FromRoute] Guid userId, CancellationToken cancellationToken = default)
    {
        string currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "SYSTEM";
        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        try
        {
            if (string.Equals(currentUserId, userId.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "操作受限",
                    Detail = "系統禁止管理員執行刪除自身的帳號動作。",
                    Instance = HttpContext.Request.Path
                });
            }

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null || user.IsDeleted)
            {
                return NotFound(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "查無使用者",
                    Detail = $"找不到識別碼為 '{userId}' 的有效使用者。",
                    Instance = HttpContext.Request.Path
                });
            }

            string originalUserName = user.UserName ?? string.Empty;
            string originalEmail = user.Email ?? string.Empty;

            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                // 1. 軟刪除狀態註記與 PII 個資抹除 (Anonymization)
                string anonymizedTag = user.Id.ToString("N")[..8];
                user.IsDeleted = true;
                user.DeletedAt = DateTimeOffset.UtcNow;
                user.DeletedBy = currentUserId;

                // 移除可識別個人資訊 (PII)，解決 Unique Index 衝突並符合 GDPR 規範
                user.UserName = $"deleted_user_{anonymizedTag}";
                user.NormalizedUserName = $"DELETED_USER_{anonymizedTag.ToUpperInvariant()}";
                user.Email = $"deleted_{anonymizedTag}@anonymized.local";
                user.NormalizedEmail = $"DELETED_{anonymizedTag.ToUpperInvariant()}@ANONYMIZED.LOCAL";
                user.PhoneNumber = null;
                user.EmailConfirmed = false;
                user.PhoneNumberConfirmed = false;
                user.TwoFactorEnabled = false;
                user.LockoutEnd = DateTimeOffset.MaxValue; // 永久封鎖

                // 解除所有已綁定角色
                var currentRoles = await _userManager.GetRolesAsync(user);
                if (currentRoles.Count > 0)
                {
                    var removeRoleResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    if (!removeRoleResult.Succeeded)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return BadRequest(new ProblemDetails
                        {
                            Status = StatusCodes.Status400BadRequest,
                            Title = "刪除使用者失敗",
                            Detail = string.Join("; ", removeRoleResult.Errors.Select(e => e.Description)),
                            Instance = HttpContext.Request.Path
                        });
                    }
                }

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return BadRequest(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "刪除使用者失敗",
                        Detail = string.Join("; ", updateResult.Errors.Select(e => e.Description)),
                        Instance = HttpContext.Request.Path
                    });
                }

                await transaction.CommitAsync(cancellationToken);

                // 2. 資安聯防：肅清被刪除使用者的所有 Token 憑證與 Redis 工作階段
                await _tokenEngine.EmergencyFreezeAsync(user.Id.ToString(), "使用者帳號已執行軟刪除與個資抹除，全面作廢憑證。");
                await _tokenEngine.CompleteRemediationAsync(user.Id.ToString());

                // 3. 寫入不可否認性資安稽核日誌 (SecurityAuditLog)
                _securityLogger.LogSecurity(
                    eventCode: "SEC-200-USER-DELETED",
                    eventCategory: "UserManagement.DeleteUser",
                    userId: currentUserId,
                    clientIp: clientIp,
                    messageTemplate: "管理員 [{AdminId}] 軟刪除並匿名化使用者帳號。目標識別碼: {TargetUserId}, 原帳號: {OriginalUserName}, 原 Email: {OriginalEmail}",
                    currentUserId,
                    userId.ToString(),
                    originalUserName,
                    originalEmail
                );

                return Ok(new { message = "使用者帳號已成功執行軟刪除與個資匿名化處理。" });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[UserManagementController] 刪除使用者作業已被用戶端取消。UserId: {UserId}", userId);
            return StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserManagementController] 軟刪除使用者時發生系統異常。UserId: {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "執行刪除使用者作業時發生系統異常，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }
}