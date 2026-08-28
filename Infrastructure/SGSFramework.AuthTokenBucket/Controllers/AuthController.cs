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
    /// <returns>包含存取權牌、選單結構與分組實驗室集合之登入結果</returns>
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

        if (string.IsNullOrWhiteSpace(request.NameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
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

        // 優先從 Body 取得 requestedLabId，若無則降級讀取 Header
        string? targetRequestedLabId = !string.IsNullOrWhiteSpace(request.RequestedLabId)
            ? request.RequestedLabId
            : Request.Headers["X-Lab-Id"].FirstOrDefault();

        try
        {
            // 1. 身分與密碼驗證
            var user = await _userManager.FindByNameAsync(request.NameOrEmail)
                       ?? await _userManager.FindByEmailAsync(request.NameOrEmail);

            if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
            {
                if (user is not null) await _userManager.AccessFailedAsync(user);
                return BuildUnauthorizedResult("帳號或密碼錯誤。");
            }

            await _userManager.ResetAccessFailedCountAsync(user);

            // 2. 透過 Service 統一進行 3-Tier 實驗室上下文初始化 (Requested -> Primary -> Active First)
            var runtimeProfile = await _runtimeScopeService.InitializeUserScopeAsync(
                user.Id.ToString(),
                targetRequestedLabId,
                cancellationToken);

            // 3. 取得可存取實驗室清單並執行 Parent 分組轉譯
            var accessibleLabsResult = await _mediator.Send(new GetAccessibleLaboratoriesQuery(user.Id.ToString()), cancellationToken);
            var flatLabs = accessibleLabsResult.IsSuccess ? accessibleLabsResult.Value : [];
 
            // 執行階層分組並自動淨化子階層 Parent 屬性
            var groupedLabs = AccessibleLabGroupDto.CreateGroupedList(flatLabs);


            // 4. 簽發 Token
            var tokenResult = await _tokenEngine.IssueInitialSessionAsync(user, deviceId, deviceName, clientIp);

            // 5. 紀錄 Security Log
            _securityLogger.LogSecurity(
                eventCode: "SEC-200-LOGIN-SUCCESS",
                eventCategory: "Auth.Login",
                userId: user.Id.ToString(),
                clientIp: clientIp,
                messageTemplate: "使用者登入成功。帳號: {Email}, 鎖定實驗室: {LabId}",
                user.Email ?? string.Empty,
                runtimeProfile.TenantLabId
            );

            return Ok(new LoginResponseDto
            {
                TokenData = tokenResult,
                Menus = runtimeProfile.Menus,
                DeviceId = deviceId,
                TenantLabId = runtimeProfile.TenantLabId.ToString(),
                GroupedLabs = groupedLabs,
                Message = "登入成功"
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "[Login-Forbidden] 登入阻斷：無實驗室存取權限。RequestedLabId: {LabId}", targetRequestedLabId);
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "無實驗室權限",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "登入處理時發生系統例外。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "系統發生非預期錯誤，請聯繫系統管理員。",
                Instance = HttpContext.Request.Path
            });
        }
    }

    /// <summary>
    /// 地端 Windows 網域單一登入
    /// </summary>
    [HttpGet("adlogin")]
    [Authorize(AuthenticationSchemes = "Windows")]
    [Function("WindowsLogin", "AD單一登入", Icon = "fa-solid fa-windows", Order = 2, Description = "內部網路 Windows 網域無感單一登入端點")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> WindowsLogin()
    {
        try
        {
            var userIdentity = HttpContext.User.Identity;
            if (userIdentity is null || !userIdentity.IsAuthenticated)
            {
                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "認證失敗",
                    Detail = "未通過 Windows 網域認證。",
                    Instance = HttpContext.Request.Path
                });
            }

            string? domainAccount = userIdentity.Name;
            if (string.IsNullOrWhiteSpace(domainAccount))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "請求無效",
                    Detail = "無法解析有效的網域帳號名稱。",
                    Instance = HttpContext.Request.Path
                });
            }

            string ssoUserName = domainAccount.Contains('\\') ? domainAccount.Split('\\')[1] : domainAccount;

            var user = await _userManager.FindByNameAsync(ssoUserName) ?? await _userManager.FindByNameAsync(domainAccount);
            if (user is null)
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
                    return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                    {
                        Status = StatusCodes.Status500InternalServerError,
                        Title = "使用者撥備失敗",
                        Detail = string.Join("; ", createResult.Errors.Select(e => e.Description)),
                        Instance = HttpContext.Request.Path
                    });
                }
            }

            string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "LOCAL-DEV-PC";
            string deviceName = Request.Headers["User-Agent"].FirstOrDefault() ?? "Chrome on Windows (Scalar Client)";
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            UserRefreshToken? existingSession = await _tokenEngine.GetActiveSessionAsync(user.Id.ToString(), deviceId);
            TokenResult? tokenResult = existingSession is not null
                ? await _tokenEngine.RefreshSessionAsync(user, deviceId, existingSession.TokenHash)
                : await _tokenEngine.IssueInitialSessionAsync(user, deviceId, deviceName, clientIp);

            if (tokenResult is null || string.IsNullOrEmpty(tokenResult.AccessToken))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "簽發失敗",
                    Detail = "認證水桶防禦熔斷：安全憑證簽發失敗。",
                    Instance = HttpContext.Request.Path
                });
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
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
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
            if (principal is null)
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
            if (user is null)
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
        ArgumentNullException.ThrowIfNull(query);

        int finalWindow = query.WindowMinutes ?? (_options.AccessTokenExpirationMinutes + 2);

        if (finalWindow is <= 0 or > 1440)
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
    /// 切換作用中的實驗室上下文 (支援自動退路與通知)
    /// </summary>
    [HttpPost("switch-context")]
    [Function("SwitchContext", "切換實驗室", Icon = "fa-solid fa-right-left", Order = 5, Description = "切換作用中的實驗室上下文，若權限不足將自動切換至主要實驗室並提醒")]
    [ProducesResponseType(typeof(SwitchLabResultDto), StatusCodes.Status200OK)]
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
            var result = await _runtimeScopeService.SwitchLaboratoryWithFallbackAsync(userId, request.TargetLabId, cancellationToken);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "[SwitchContext-Forbidden] 使用者 {UserId} 切換實驗室失敗：完全缺乏可用權限。", userId);

            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "權限不足",
                Detail = ex.Message,
                Instance = HttpContext.Request.Path
            });
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
    /// 取得當前使用者所有已登入的裝置與工作階段清單
    /// </summary>
    [HttpGet("sessions")]
    [Authorize]
    [Function("GetActiveSessions", "取得線上裝置清單", Icon = "fa-solid fa-laptop-code", Order = 8, Description = "獲取當前使用者所有已登入的裝置與 Session 清單")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetActiveSessionsAsync(CancellationToken cancellationToken = default)
    {
        string? userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "未授權存取",
                Instance = HttpContext.Request.Path
            });
        }

        string currentDeviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "UNKNOWN-DEVICE";

        try
        {
            // 呼叫 Repository 取得目前使用者的所有 Active Sessions
            var sessions = await _tokenRepository.GetActiveSessionsAsync(userId, cancellationToken);

            var sessionDtos = sessions.Select(s => new
            {
                s.DeviceId,
                s.DeviceName,
                s.ClientIp,
                s.CreatedAt,
                s.LastActiveAt,
                IsCurrent = string.Equals(s.DeviceId, currentDeviceId, StringComparison.OrdinalIgnoreCase)
            });

            return Ok(sessionDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "獲取使用者的所有裝置工作階段清單時發生異常。UserId: {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "無法獲取裝置清單。",
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
        string? userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "未授權存取",
                Instance = HttpContext.Request.Path
            });
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
                Detail = "登出程序執行失敗。",
                Instance = HttpContext.Request.Path
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
        string? userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "未授權存取",
                Instance = HttpContext.Request.Path
            });
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
                Detail = "全裝置登出程序執行失敗。",
                Instance = HttpContext.Request.Path
            });
        }
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
}