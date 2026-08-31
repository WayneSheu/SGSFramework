using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Models;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Controllers.Base;

namespace SGSFramework.Identity.Controllers.v1;

/// <summary>
/// 使用者身分驗證與帳號管理控制器
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Produces(MediaTypeNames.Application.Json)]
[Consumes(MediaTypeNames.Application.Json)]
[ControllerTitle("使用者管理", Icon = "fa-solid fa-user-gear", Order = 10, Description = "提供使用者註冊、身分驗證、2FA、密碼安全維護與工作階段管理服務")]
[RequiresPermission("SYSTEM.USERMANAGEMENT")]
public sealed class UserManagementController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    TokenBucketEngine<ApplicationUser> tokenEngine,
    ILogger<UserManagementController> logger) : ApiControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
    private readonly TokenBucketEngine<ApplicationUser> _tokenEngine = tokenEngine ?? throw new ArgumentNullException(nameof(tokenEngine));
    private readonly ILogger<UserManagementController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// 使用者註冊 (支援自訂帳號與 Email 雙重唯一性校驗)
    /// </summary>
    /// <param name="request">註冊請求內容</param>
    /// <returns>註冊結果與驗證資訊</returns>
    [HttpPost("register")]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.REGISTER")]
    [Function("Register", "使用者註冊", Icon = "fa-solid fa-user-plus", Order = 1, Description = "進行新使用者帳號註冊並生成電子郵件驗證憑證")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

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
            _logger.LogInformation("用戶 [{Username}] 註冊成功，已生成驗證信憑證。", user.UserName);

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
    /// <param name="email">電子郵件</param>
    /// <param name="token">驗證記號</param>
    /// <returns>電子郵件驗證結果</returns>
    [HttpGet("confirm-email")]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.CONFIRMEMAIL")]
    [Function("ConfirmEmail", "使用者 Email 驗證", Icon = "fa-solid fa-envelope-circle-check", Order = 2, Description = "檢驗使用者電子郵件驗證碼並啟用帳號")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
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
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "驗證失敗",
                    Detail = "電子郵件驗證失敗或記號已過期。",
                    Instance = HttpContext.Request.Path
                });
            }

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

    ///// <summary>
    ///// 登入端點 (支援自訂帳號或 Email 雙軌識別 + 2FA 分流)
    ///// </summary>
    ///// <param name="request">登入請求內容</param>
    ///// <returns>工作階段 Token 憑證或 2FA 挑戰指示</returns>
    //[HttpPost("login")]
    //[Function("Login", "一般帳號登入", Icon = "fa-solid fa-right-to-bracket", Order = 3, Description = "使用者登入驗證，支援帳號/Email 雙軌登入與 2FA 二次驗證流程")]
    //[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    //[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    //[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    //[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    //public async Task<IActionResult> Login([FromBody] ManagementLoginRequest request)
    //{
    //    ArgumentNullException.ThrowIfNull(request);

    //    try
    //    {
    //        var user = await _userManager.FindByNameAsync(request.AccountIdentifier)
    //                   ?? await _userManager.FindByEmailAsync(request.AccountIdentifier);

    //        if (user == null)
    //        {
    //            return Unauthorized(new ProblemDetails
    //            {
    //                Status = StatusCodes.Status401Unauthorized,
    //                Title = "身份驗證失敗",
    //                Detail = "帳號、Email 或密碼錯誤。",
    //                Instance = HttpContext.Request.Path
    //            });
    //        }

    //        if (await _userManager.IsLockedOutAsync(user))
    //        {
    //            return StatusCode(StatusCodes.Status423Locked, new ProblemDetails
    //            {
    //                Status = StatusCodes.Status423Locked,
    //                Title = "帳號已鎖定",
    //                Detail = "帳號因連續登入失敗已被系統鎖定，請稍後再試。",
    //                Instance = HttpContext.Request.Path
    //            });
    //        }

    //        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

    //        if (result.IsLockedOut)
    //        {
    //            _logger.LogWarning("用戶 {Account} 觸發連續失敗鎖定。", user.UserName);
    //            return StatusCode(StatusCodes.Status423Locked, new ProblemDetails
    //            {
    //                Status = StatusCodes.Status423Locked,
    //                Title = "帳號已鎖定",
    //                Detail = "密碼錯誤次數過多，帳號已被暫時鎖定。",
    //                Instance = HttpContext.Request.Path
    //            });
    //        }

    //        if (result.RequiresTwoFactor)
    //        {
    //            string code = await _userManager.GenerateTwoFactorTokenAsync(user, "Email");
    //            return Ok(new { requiresTwoFactor = true, message = "已啟用雙因子驗證，請輸入郵件驗證碼。", debug2FaCode = code });
    //        }

    //        if (!result.Succeeded)
    //        {
    //            return Unauthorized(new ProblemDetails
    //            {
    //                Status = StatusCodes.Status401Unauthorized,
    //                Title = "身份驗證失敗",
    //                Detail = "帳號、Email 或密碼錯誤。",
    //                Instance = HttpContext.Request.Path
    //            });
    //        }

    //        string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "UNKNOWN-DEVICE";
    //        string deviceName = Request.Headers["User-Agent"].FirstOrDefault() ?? "Generic Browser";
    //        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    //        var tokenResult = await _tokenEngine.IssueInitialSessionAsync(user, deviceId, deviceName, clientIp);

    //        return Ok(new { message = "登入成功", tokenData = tokenResult });
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "登入端點發生未預期異常。");
    //        return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
    //        {
    //            Status = StatusCodes.Status500InternalServerError,
    //            Title = "伺服器內部錯誤",
    //            Detail = "登入處理期間發生系統異常，請聯繫系統管理員。",
    //            Instance = HttpContext.Request.Path
    //        });
    //    }
    //}

    /// <summary>
    /// 驗證雙因子登入 (2FA)
    /// </summary>
    /// <param name="request">2FA 驗證請求內容</param>
    /// <returns>工作階段 Token 憑證</returns>
    [HttpPost("verify-2fa")]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.VERIFYTWOFACTOR")]
    [Function("VerifyTwoFactor", "雙因子驗證登入", Icon = "fa-solid fa-key", Order = 4, Description = "驗證使用者雙因子驗證碼 (2FA) 並簽發正式工作階段憑證")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] TwoFactorVerificationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

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
                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "驗證碼無效",
                    Detail = "雙因子驗證碼不正確或已逾期。",
                    Instance = HttpContext.Request.Path
                });
            }

            await _userManager.ResetAccessFailedCountAsync(user);

            string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "UNKNOWN-DEVICE";
            string deviceName = Request.Headers["User-Agent"].FirstOrDefault() ?? "Generic Browser";
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var tokenResult = await _tokenEngine.IssueInitialSessionAsync(user, deviceId, deviceName, clientIp);

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
    /// <param name="request">忘記密碼請求內容</param>
    /// <returns>申請處理訊息</returns>
    [HttpPost("forgot-password")]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.FORGOTPASSWORD")]
    [Function("ForgotPassword", "忘記密碼", Icon = "fa-solid fa-unlock-keyhole", Order = 5, Description = "發送密碼重設郵件與記號至使用者信箱")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
            {
                return Ok(new { message = "若帳號存在且已完成啟用，重設密碼信件已發送至您的信箱。" });
            }

            string resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            _logger.LogInformation("用戶 {Email} 申請密碼重設記號成功。", request.Email);

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
    /// <param name="request">重設密碼請求內容</param>
    /// <returns>重設結果與安全通告</returns>
    [HttpPost("reset-password")]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.RESETPASSWORD")]
    [Function("ResetPassword", "重設密碼", Icon = "fa-solid fa-shield-cat", Order = 6, Description = "使用重設記號重置密碼，並強制作廢全網歷史 Session 憑證")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

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
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "重設失敗",
                    Detail = "密碼重設失敗，可能記號已失效或新密碼不符合複雜度規範。",
                    Instance = HttpContext.Request.Path
                });
            }

            // 雙軌資安聯防：強制登出該用戶全網所有其餘工作階段
            await _tokenEngine.EmergencyFreezeAsync(user.Id.ToString(), "使用者透過忘記密碼功能完成密碼重設，全面肅清舊有憑證軌跡。");
            await _tokenEngine.CompleteRemediationAsync(user.Id.ToString());

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
    /// <param name="request">變更密碼請求內容</param>
    /// <returns>變更結果與安全通告</returns>
    [Authorize]
    [HttpPost("change-password")]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.CHANGEPASSWORD")]
    [Function("ChangePassword", "變更密碼", Icon = "fa-solid fa-lock-rotate", Order = 7, Description = "使用者登入狀態下變更密碼，並觸發資安聯防註銷其他裝置 Session")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

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
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "變更密碼失敗",
                    Detail = "變更密碼失敗，請確認舊密碼是否輸入正確或符合密碼複雜度原則。",
                    Instance = HttpContext.Request.Path
                });
            }

            // 資安聯防：變更密碼成功後，立使所有舊 JWT 與長效 Refresh Token 失敗，防禦 Session 劫持
            await _tokenEngine.EmergencyFreezeAsync(user.Id.ToString(), "使用者執行線上變更密碼，強制登出全網所有裝置工作階段。");
            await _tokenEngine.CompleteRemediationAsync(user.Id.ToString());

            _logger.LogInformation("用戶 [{Username}] 已成功在線上變更密碼並肅清全網 Session。", user.UserName);

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
    /// <returns>登出結果</returns>
    [Authorize]
    [HttpPost("logout")]
    [RequiresPermission("SYSTEM.USERMANAGEMENT.LOGOUT")]
    [Function("Logout", "安全登出", Icon = "fa-solid fa-right-from-bracket", Order = 8, Description = "安全登出系統並註銷當前裝置之活動工作階段票據")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Logout()
    {
        try
        {
            string userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "UNKNOWN-DEVICE";

            if (!string.IsNullOrEmpty(userId))
            {
                await _tokenEngine.EmergencyFreezeAsync(userId, $"用戶主動執行安全登出。裝置識別: {deviceId}");
                await _tokenEngine.CompleteRemediationAsync(userId);
                _logger.LogInformation("用戶 {UserId} 自裝置 {DeviceId} 安全登出。", userId, deviceId);
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
}

#region DTO 模型定義載體

public sealed class RegisterRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class ManagementLoginRequest
{
    public string AccountIdentifier { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class TwoFactorVerificationRequest
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public sealed class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    public string Email { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

#endregion