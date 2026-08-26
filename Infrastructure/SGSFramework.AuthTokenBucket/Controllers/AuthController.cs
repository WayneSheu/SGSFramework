using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGS.Modules.ORG.Contracts.Queries;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Configurations;
using SGSFramework.AuthTokenBucket.DTOs;
using SGSFramework.AuthTokenBucket.Models;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Logings;
using SGSFramework.Core.Abstractions.Menus;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Core.DTOs;
using SGSFramework.Core.HttpAuditProviders;
using System.Security.Claims;

namespace SGSFramework.AuthTokenBucket.Controllers.v1;

/// <summary>
/// 身份驗證與 Token 管理控制器
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
[ControllerTitle("身份驗證", Icon = "fa-solid fa-user-lock", Order = 10, Description = "提供帳密登入、AD SSO 登入、Token 輪轉刷新、動態選單與實驗室上下文切換服務")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    TokenManager tokenManager,
    ITokenStorageProvider storageProvider,
    IUserRefreshTokenRepository tokenRepository,
    TokenBucketEngine<ApplicationUser> tokenEngine,
    IOptions<AuthTokenBucketOptions> options,
    ILogger<AuthController> logger,
    IAuditProvider auditProvider,
    ISecurityLogger securityLogger,
    IDynamicMenuService menuService,
    IUserRuntimeScopeService runtimeScopeService,
    ISender mediator) : ApiControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    private readonly TokenManager _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
    private readonly ITokenStorageProvider _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
    private readonly IUserRefreshTokenRepository _tokenRepository = tokenRepository ?? throw new ArgumentNullException(nameof(tokenRepository));
    private readonly TokenBucketEngine<ApplicationUser> _tokenEngine = tokenEngine ?? throw new ArgumentNullException(nameof(tokenEngine));
    private readonly AuthTokenBucketOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    private readonly ILogger<AuthController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IAuditProvider _auditProvider = auditProvider ?? throw new ArgumentNullException(nameof(auditProvider));
    private readonly ISecurityLogger _securityLogger = securityLogger ?? throw new ArgumentNullException(nameof(securityLogger));
    private readonly IDynamicMenuService _menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
    private readonly IUserRuntimeScopeService _runtimeScopeService = runtimeScopeService ?? throw new ArgumentNullException(nameof(runtimeScopeService));
    private readonly ISender _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));

    /// <summary>
    /// 標準帳密登入
    /// </summary>
    /// <param name="request">登入憑證資訊</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>包含存取權牌、選單結構與權限集合之登入結果</returns>
    [HttpPost("login")]
    [Function("Login", "帳密登入", Icon = "fa-solid fa-right-to-bracket", Order = 1, Description = "標準帳密登入端點，整合大容量 Bitmask 權限與 TokenBucketEngine 基礎設施")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status423Locked)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LoginAsync(
        [FromBody] LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. DTO 驗證檢查
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "無效的請求數據",
                Detail = "帳號與密碼欄位不可為空。",
                Instance = HttpContext.Request.Path
            });
        }

        string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "UNKNOWN-DEVICE";
        string deviceName = Request.Headers["User-Agent"].FirstOrDefault() ?? "Generic Browser";
        string? requestedLabId = Request.Headers["X-Lab-Id"].FirstOrDefault();

        try
        {
            // 2. 查詢使用者 (支援帳號或 Email)
            var user = await _userManager.FindByNameAsync(request.Email)
                       ?? await _userManager.FindByEmailAsync(request.Email);

            if (user == null)
            {
                LogLoginFailure(request.Email, clientIp, "使用者不存在");
                return BuildUnauthorizedResult("帳號或密碼錯誤。");
            }

            // 3. 檢查帳號狀態：是否鎖定 (Lockout)
            if (await _userManager.IsLockedOutAsync(user))
            {
                _securityLogger.LogSecurity(
                    eventCode: "SEC-423-LOCKED",
                    eventCategory: "Auth.Login",
                    userId: user.Id.ToString(),
                    clientIp: clientIp,
                    messageTemplate: "帳號已被暫時鎖定。帳號: {Email}, 來源IP: {ClientIp}",
                    user.Email ?? request.Email,
                    clientIp
                );

                return StatusCode(StatusCodes.Status423Locked, new ProblemDetails
                {
                    Status = StatusCodes.Status423Locked,
                    Title = "帳號已被鎖定",
                    Detail = "由於連續嘗試失敗次數過多，帳號已被暫時鎖定，請稍後再試。",
                    Instance = HttpContext.Request.Path
                });
            }

            // 4. 檢查密碼與登入嘗試紀錄
            bool isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!isPasswordValid)
            {
                await _userManager.AccessFailedAsync(user);
                LogLoginFailure(request.Email, clientIp, "密碼比對失敗");

                return BuildUnauthorizedResult("帳號或密碼錯誤。");
            }

            // 5. 重置失敗計數器 (密碼正確)
            await _userManager.ResetAccessFailedCountAsync(user);

            // 6. 透過 MediatR 遠端解耦呼叫 ORG 模組取得可存取實驗室清單 (自動支援 Sysadmin 全域存取)
            var accessibleLabsResult = await _mediator.Send(new GetAccessibleLaboratoriesQuery(user.Id.ToString()), cancellationToken);
            var accessibleLabs = accessibleLabsResult.IsSuccess ? accessibleLabsResult.Value : [];

            // 7. 驗證並解析請求端指定的實驗室上下文
            Guid targetLabGuid = Guid.Empty;
            if (!string.IsNullOrWhiteSpace(requestedLabId) && Guid.TryParse(requestedLabId, out Guid parsedLabId))
            {
                if (accessibleLabs.Any(l => l.LabId == parsedLabId))
                {
                    targetLabGuid = parsedLabId;
                }
            }

            if (targetLabGuid == Guid.Empty && accessibleLabs.Any())
            {
                targetLabGuid = accessibleLabs.First().LabId;
            }

            // 8. 簽發 Token
            var tokenResult = await _tokenEngine.IssueInitialSessionAsync(user, deviceId, deviceName, clientIp);

            // 9. 初始化 Runtime Scope 與選單資料
            var runtimeProfile = await _runtimeScopeService.InitializeUserScopeAsync(
                user.Id.ToString(),
                targetLabGuid == Guid.Empty ? null : targetLabGuid.ToString(),
                cancellationToken);

            // 10. 紀錄成功的 Security/Audit 日誌
            _securityLogger.LogSecurity(
                eventCode: "SEC-200-LOGIN-SUCCESS",
                eventCategory: "Auth.Login",
                userId: user.Id.ToString(),
                clientIp: clientIp,
                messageTemplate: "使用者登入成功。帳號: {Email}, 裝置ID: {DeviceId}, 來源IP: {ClientIp}",
                user.Email ?? string.Empty,
                deviceId,
                clientIp
            );

            return Ok(new LoginResponseDto
            {
                TokenData = tokenResult,
                Menus = runtimeProfile.Menus,
                DeviceId = deviceId,
                LabId = targetLabGuid.ToString(),
                AccessibleLabs = accessibleLabs,
                Message = "登入成功"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "標準帳密登入端點發生未預期核心異常。帳號: {TargetUser}, IP: {ClientIp}", request.Email, clientIp);

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "系統執行身份驗證時發生未預期錯誤，請聯絡系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    #region Private Helper Methods

    private void LogLoginFailure(string targetUser, string clientIp, string reason)
    {
        _securityLogger.LogSecurity(
            eventCode: "SEC-401-UNAUTHORIZED",
            eventCategory: "Auth.Login",
            userId: targetUser,
            clientIp: clientIp,
            messageTemplate: "登入失敗 ({Reason})。帳號: {TargetUser}, 來源IP: {ClientIp}",
            reason,
            targetUser,
            clientIp
        );
    }

    private UnauthorizedObjectResult BuildUnauthorizedResult(string detailMessage)
    {
        return Unauthorized(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "身分驗證失敗",
            Detail = detailMessage,
            Instance = HttpContext.Request.Path
        });
    }

    #endregion

    /// <summary>
    /// 地端 Windows 網域單一登入
    /// </summary>
    [HttpGet("adlogin")]
    [Authorize(AuthenticationSchemes = "Windows")]
    [Function("WindowsLogin", "AD單一登入", Icon = "fa-solid fa-windows", Order = 2, Description = "內部網路 Windows 網域無感單一登入端點")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> WindowsLogin()
    {
        try
        {
            var userIdentity = HttpContext.User.Identity;
            if (userIdentity == null || !userIdentity.IsAuthenticated)
            {
                return Unauthorized(new { message = "未通過 Windows 網域認證。" });
            }

            string? domainAccount = userIdentity.Name;
            if (string.IsNullOrWhiteSpace(domainAccount))
            {
                return BadRequest(new { message = "無法解析有效的網域帳號名稱。" });
            }

            string ssoUserName = domainAccount.Contains('\\') ? domainAccount.Split('\\')[1] : domainAccount;

            var user = await _userManager.FindByNameAsync(ssoUserName) ?? await _userManager.FindByNameAsync(domainAccount);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = ssoUserName,
                    Email = $"{ssoUserName.ToLower()}@company.com",
                    EmailConfirmed = true,
                    LockoutEnabled = false
                };

                var createResult = await _userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new
                    {
                        message = "JIT 自動撥備使用者失敗",
                        errors = createResult.Errors.Select(e => e.Description)
                    });
                }
            }

            string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "LOCAL-DEV-PC";
            string deviceName = Request.Headers["User-Agent"].FirstOrDefault() ?? "Chrome on Windows (Scalar Client)";
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            UserRefreshToken? existingSession = await _tokenEngine.GetActiveSessionAsync(user.Id.ToString(), deviceId);
            TokenResult? tokenResult;

            if (existingSession != null)
            {
                tokenResult = await _tokenEngine.RefreshSessionAsync(user, deviceId, existingSession.TokenHash);
            }
            else
            {
                tokenResult = await _tokenEngine.IssueInitialSessionAsync(user, deviceId, deviceName, clientIp);
            }

            if (tokenResult == null || string.IsNullOrEmpty(tokenResult.AccessToken))
            {
                return StatusCode(StatusCodes.Status400BadRequest, new { message = "認證水桶防禦熔斷：安全憑證簽發失敗。" });
            }

            return Ok(new
            {
                Message = "Windows 網域單一登入成功",
                Identity = domainAccount,
                SanitizedUserName = user.UserName,
                TokenData = tokenResult
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Windows AD 認證處理時發生異常。");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Windows 認證處理器發生核心異常",
                detail = ex.Message,
                innerException = ex.InnerException?.Message
            });
        }
    }

    /// <summary>
    /// 雙向權限票據高併發輪轉刷新
    /// </summary>
    [HttpPost("refresh")]
    [Function("RefreshToken", "刷新Token", Icon = "fa-solid fa-arrows-rotate", Order = 3, Description = "雙向權限票據高併發輪轉刷新端點")]
    [ProducesResponseType(typeof(TokenResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RefreshTokenAsync(
        [FromBody] RefreshTokenRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var principal = _tokenManager.GetPrincipalFromExpiredToken(request.AccessToken);
            if (principal == null)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "請求票據無效",
                    Detail = "無效的存取記號 (Invalid Access Token)。",
                    Instance = HttpContext.Request.Path
                });
            }

            string userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "使用者無效",
                    Detail = "使用者不存在或已被刪除。",
                    Instance = HttpContext.Request.Path
                });
            }

            string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "UNKNOWN-DEVICE";
            var tokenResult = await _tokenEngine.RefreshSessionAsync(user, deviceId, request.RefreshToken);

            return Ok(tokenResult);
        }
        catch (Microsoft.IdentityModel.Tokens.SecurityTokenException ex) when (ex.Message == "TOKEN_REPLAY_ATTACK_DETECTED")
        {
            _securityLogger.LogSecurity(
                eventCode: "SEC-401-TOKEN_REPLAY_ATTACK_DETECTED",
                eventCategory: "Auth.TokenRefresh",
                userId: UserInfo.UserId ?? "Unknown",
                clientIp: UserInfo.ClientIp ?? "0.0.0.0",
                messageTemplate: "偵測到 Token 重放攻擊熔斷！用戶識別碼: {UserId}, 來源IP: {ClientIp}",
                UserInfo.UserId ?? "Unknown",
                UserInfo.ClientIp ?? "0.0.0.0"
            );

            return StatusCode(StatusCodes.Status401Unauthorized, new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "資安威脅熔斷",
                Detail = "憑證異常，檢測到潛在的重放安全威脅，已強制終止所有連線。",
                Instance = HttpContext.Request.Path
            });
        }
        catch (Microsoft.IdentityModel.Tokens.SecurityTokenException ex) when (ex.Message == "ACCOUNT_FROZEN_OR_INVALID_SESSION")
        {
            _securityLogger.LogSecurity(
                eventCode: "SEC-401-INVALID-SESSION",
                eventCategory: "Auth.TokenRefresh",
                userId: UserInfo.UserId ?? "Unknown",
                clientIp: UserInfo.ClientIp ?? "0.0.0.0",
                messageTemplate: "帳號已凍結或工作階段無效。用戶識別碼: {UserId}, 來源IP: {ClientIp}",
                UserInfo.UserId ?? "Unknown",
                UserInfo.ClientIp ?? "0.0.0.0"
            );

            return StatusCode(StatusCodes.Status401Unauthorized, new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "工作階段失效",
                Detail = "工作階段已失效或帳戶已被鎖定。",
                Instance = HttpContext.Request.Path
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新 Token 時發生核心異常。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "刷新 Token 時發生核心異常。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 獲取線上即時活動用戶數觀測
    /// </summary>
    [HttpGet("online-count")]
    [Function("GetOnlineUserCount", "線上人數統計", Icon = "fa-solid fa-users", Order = 4, Description = "獲取線上即時活動用戶數觀測端點")]
    [ProducesResponseType(typeof(OnlineUserCountResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetOnlineUserCountAsync(
        [FromQuery] OnlineUserCountQueryDto query,
        CancellationToken cancellationToken = default)
    {
        int finalWindow = query.WindowMinutes ?? (_options.AccessTokenExpirationMinutes + 2);

        if (finalWindow <= 0 || finalWindow > 1440)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "參數超出範圍",
                Detail = "活動觀測時間視窗必須介於 1 到 1440 分鐘之間。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            int activeUsers = await _tokenRepository.GetActiveOnlineUserCountAsync(finalWindow);

            return Ok(new OnlineUserCountResponseDto
            {
                ActiveOnlineUsers = activeUsers,
                ObservationWindowMinutes = finalWindow,
                IsUsingDefaultConfigWindow = !query.WindowMinutes.HasValue,
                CalculatedAtUtc = DateTime.UtcNow
            });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "系統操作無效",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取線上人數統計時發生未知錯誤。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "內部資料統計異常",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 切換作用中的實驗室上下文 (無感切換)
    /// </summary>
    [HttpPost("switch-context")]
    [Function("SwitchContext", "切換實驗室", Icon = "fa-solid fa-right-left", Order = 5, Description = "高階主管切換作用中的實驗室上下文")]
    [ProducesResponseType(typeof(UserPermissionProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SwitchContextAsync(
        [FromBody] SwitchLabRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "身份驗證失敗",
                Detail = "無法識別當前使用者的身份上下文。",
                Instance = HttpContext.Request.Path
            });
        }

        try
        {
            var newProfile = await _runtimeScopeService.SwitchLaboratoryAsync(userId, request.TargetLabId, cancellationToken);

            if (newProfile is null)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "切換實驗室失敗",
                    Detail = "目標實驗室不存在，或您無權存取該實驗室。",
                    Instance = HttpContext.Request.Path
                });
            }

            return Ok(newProfile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "切換實驗室上下文時發生未預期異常。UserId: {UserId}, TargetLabId: {LabId}", userId, request.TargetLabId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "切換實驗室上下文失敗。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 單一裝置登出
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [Function("Logout", "單一登出", Icon = "fa-solid fa-right-from-bracket", Order = 6, Description = "終止當前裝置的工作階段與 Refresh Token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ProblemDetails { Status = StatusCodes.Status401Unauthorized, Title = "未授權存取" });
        }

        string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "UNKNOWN-DEVICE";

        try
        {
            await _tokenRepository.RevokeSessionAsync(userId, deviceId);
            _logger.LogInformation("使用者 {UserId} 在裝置 {DeviceId} 執行單一登出成功。", userId, deviceId);

            return Ok(new { Message = "單一裝置登出成功。" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "單一裝置登出時發生異常。UserId: {UserId}, DeviceId: {DeviceId}", userId, deviceId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "登出程序執行失敗。"
            });
        }
    }

    /// <summary>
    /// 所有裝置登出
    /// </summary>
    [HttpPost("logout-all")]
    [Authorize]
    [Function("LogoutAll", "所有裝置登出", Icon = "fa-solid fa-power-off", Order = 7, Description = "強制終止該使用者所有裝置的有效 Token 與工作階段")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LogoutAllAsync(CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ProblemDetails { Status = StatusCodes.Status401Unauthorized, Title = "未授權存取" });
        }

        try
        {
            await _tokenRepository.RevokeAllUserSessionsAsync(userId);

            _securityLogger.LogSecurity(
                eventCode: "SEC-200-LOGOUT-ALL",
                eventCategory: "Auth.Logout",
                userId: userId,
                clientIp: HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0",
                messageTemplate: "使用者強制終止所有裝置工作階段。用戶識別碼: {UserId}",
                userId
            );

            return Ok(new MessageResponseDto { Message = "已成功登出所有裝置。" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "所有裝置登出時發生異常。UserId: {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "全裝置登出程序執行失敗。"
            });
        }
    }
}