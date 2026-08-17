using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Configurations;
using SGSFramework.AuthTokenBucket.DTOs;
using SGSFramework.AuthTokenBucket.Models;
using SGSFramework.AuthTokenBucket.Servers;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Logings;
using SGSFramework.Core.Abstractions.Menus;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Core.DTOs;
using SGSFramework.Core.HttpAuditProviders;
using System.ComponentModel;
using System.Security.Claims;

namespace SGSFramework.AuthTokenBucket.Controllers.v1;

/// <summary>
/// 身份驗證與 Token 管理控制器
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
[Menu("身份驗證", "fa-solid fa-user-lock", order: 1, parent: null)]
[Description("提供帳密登入、Token 輪轉刷新、動態選單與實驗室上下文切換服務")]
public sealed class AuthController : ApiControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TokenManager _tokenManager;
    private readonly ITokenStorageProvider _storageProvider;
    private readonly IUserRefreshTokenRepository _tokenRepository;
    private readonly TokenBucketEngine<ApplicationUser> _tokenEngine; 
    private readonly AuthTokenBucketOptions _options;
    private readonly ILogger<AuthController> _logger;
    private readonly IAuditProvider _auditProvider;
    private readonly ISecurityLogger _securityLogger;
    private readonly IDynamicMenuService _menuService;
    private readonly IUserRuntimeScopeService _runtimeScopeService;

    public AuthController(
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
        IUserRuntimeScopeService runtimeScopeService)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
        _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
        _tokenRepository = tokenRepository ?? throw new ArgumentNullException(nameof(tokenRepository));
        _tokenEngine = tokenEngine ?? throw new ArgumentNullException(nameof(tokenEngine));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditProvider = auditProvider ?? throw new ArgumentNullException(nameof(auditProvider));
        _securityLogger = securityLogger ?? throw new ArgumentNullException(nameof(securityLogger));
        _menuService = menuService ?? throw new ArgumentNullException(nameof(menuService));
        _runtimeScopeService = runtimeScopeService ?? throw new ArgumentNullException(nameof(runtimeScopeService));
    }

    /// <summary>
    /// 標準帳密登入
    /// </summary>
    /// <param name="request">登入憑證資訊</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>包含存取權牌、選單結構與權限集合之登入結果</returns>
    [HttpPost("login")]
    [Menu("帳密登入", "fa-solid fa-right-to-bracket", order: 1, parent: "身份驗證")]
    [Description("標準帳密登入端點，整合大容量 Bitmask 權限與 TokenBucketEngine 基礎設施")]
    [ProducesResponseType(typeof(LoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LoginAsync(
        [FromBody] LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // 1. 基礎身份認證
            var user = await _userManager.FindByNameAsync(request.Email) ?? await _userManager.FindByEmailAsync(request.Email) ;
            if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            {
                _logger.LogWarning(
                    "[Security-Event:{EventCode}] 登入失敗：帳號或密碼錯誤。帳號: {TargetUser}, 來源IP: {RemoteIp}",
                    "SEC-401-Unauthorized",
                    request.Email,
                    HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
                );

                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "身分驗證失敗",
                    Detail = "帳號或密碼錯誤。",
                    Instance = HttpContext.Request.Path
                });
            }
            // 2. 獲取請求特徵 (包含 DeviceId 與 LabId)
            string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "UNKNOWN-DEVICE";
            string deviceName = Request.Headers["User-Agent"].FirstOrDefault() ?? "Generic Browser";
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            // 優先從 Header 取得請求的 LabId，若無則由 RuntimeScopeService 決定預設值
            string requestedLabId = Request.Headers["X-Lab-Id"].FirstOrDefault();
            // 3. 簽發 Token
            var tokenResult = await _tokenEngine.IssueInitialSessionAsync(user, deviceId, deviceName, clientIp);
            // 4. 初始化 Runtime Scope
            // 傳入 requestedLabId，讓 Service 判斷該使用者是否有權進入該實驗室
            // 呼叫初始化服務
            var runtimeProfile = await _runtimeScopeService.InitializeUserScopeAsync(
                user.Id.ToString(),
                requestedLabId, // 從 Header 來的 string
                cancellationToken);

            // 直接在 DTO 使用服務回傳的結果
            return Ok(new LoginResponseDto
            {
                TokenData = tokenResult,
                Menus = runtimeProfile.Menus, // 從 Profile 取得
                DeviceId = deviceId,
                LabId = runtimeProfile.LabId.ToString(), // 同步正確的 LabId
                Message = "登入成功"
            });


            _logger.LogInformation("使用者 {Email} 登入成功，裝置 ID: {DeviceId}", user.Email, deviceId);
            
            // 1. 從 UserRuntimeScopeService 取得實質權限清單
            IEnumerable<string> userPermissions = await _runtimeScopeService.GetUserPermissionsAsync(
                user.Id.ToString(),
                cancellationToken: cancellationToken);

            // 2. 傳入權限集合，算出一套帶有 Level 與 IsDisplay 控制標記的階層選單
            var menuTree = await _menuService.GetUserMenuAsync(userPermissions);

            return Ok(new LoginResponseDto
            {
                TokenData = tokenResult,
                Menus = runtimeProfile.Menus,
                DeviceId = deviceId,
                LabId = runtimeProfile.LabId.ToString(),
                AccessibleLabs = runtimeProfile.AccessibleLabs, // 帶入可存取清單
                Message = "登入成功"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "標準帳密登入端點發生未預期核心異常。");
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
    /// 地端 Windows 網域單一登入
    /// </summary>
    [HttpGet("adlogin")]
    [Authorize(AuthenticationSchemes = "Windows")]
    [Menu("AD登入", "fa-solid fa-lock", order: 1, parent: "身份驗證")]
    [Description("內部網路 Windows 網域無感單一登入端點（全面整合 TokenManager 與大容量 Bitmask）")]
    public async Task<IActionResult> WindowsLogin()
    {
        try
        {
            var userIdentity = HttpContext.User.Identity;
            if (userIdentity == null || !userIdentity.IsAuthenticated)
            {
                return Unauthorized(new { message = "未通過 Windows 網域認證。" });
            }

            string domainAccount = userIdentity.Name;
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
            TokenResult tokenResult;

            if (existingSession != null)
            {
                // 若存在活體工作階段，直接調用引擎換票分流或重組真實憑證
                tokenResult = await _tokenEngine.RefreshSessionAsync(user, deviceId, existingSession.TokenHash);
            }
            else
            {
                // 🚀 執行初次建立：整合大容量 Bitmask 權限與 TokenManager 簽發實體 JWT 票據
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
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Windows 認證處理器發生核心異常",
                detail = ex.Message,
                innerException = ex.InnerException?.Message,
                stackTrace = ex.StackTrace
            });
        }
    }


    /// <summary>
    /// 雙向權限票據高併發輪轉刷新
    /// </summary>
    /// <param name="request">刷新 Token 請求內容</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>更新後之 Token 數據集</returns>
    [HttpPost("refresh")]
    [Menu("刷新 Token", "fa-solid fa-arrows-rotate", order: 2, parent: "身份驗證")]
    [Description("雙向權限票據高併發輪轉刷新端點，對齊更新後的 TokenBucketEngine 參數規範")]
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
    /// <param name="query">線上人數查詢過濾參數</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>線上即時活動用戶統計資料</returns>
    [HttpGet("online-count")]
    [Menu("線上人數統計", "fa-solid fa-users", order: 3, parent: "身份驗證")]
    [Description("獲取線上即時活動用戶數觀測端點，支援自訂觀測時間視窗")]
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
    /// <param name="request">目標實驗室切換請求</param>
    /// <param name="cancellationToken">異步取消權牌</param>
    /// <returns>切換後的新權限配置與選單結構</returns>
    [HttpPost("switch-context")]
    [Menu("切換實驗室", "fa-solid fa-exchange-alt", order: 4, parent: "身份驗證")]
    [Description("高階主管切換作用中的實驗室上下文，支援無感切換")]
    [ProducesResponseType(typeof(UserPermissionProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SwitchContextAsync([FromBody] SwitchLabRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. 從 HttpContext 獲取當前使用者識別碼
        string? userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value;
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
            // 2. 呼叫服務層執行實驗室切換
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
    /// 單一裝置登出（依據當前請求帶入的 DeviceId 與使用者身分）
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [Menu("單一登出", "fa-solid fa-right-from-bracket", order: 5, parent: "身份驗證")]
    [Description("終止當前裝置的工作階段與 Refresh Token")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken = default)
    {
        // 直接從 HttpContext 延伸方法取得原始的 Access Token
        string? rawToken = await HttpContext.GetTokenAsync("access_token");

        // 1. 從 ClaimsPrincipal 取得當前使用者識別碼
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ProblemDetails { Status = 401, Title = "未授權存取" });
        }

        // 從 Request Header 取得當前裝置識別碼（與登入時保持一致的維度）
        string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "UNKNOWN-DEVICE";

        try
        {
            // 透過 Token 引擎或 Repository 將該使用者在特定裝置的 Session 註銷
            await _tokenRepository.RevokeSessionAsync(userId, deviceId);

            _logger.LogInformation("使用者 {UserId} 在裝置 {DeviceId} 執行單一登出成功。", userId, deviceId);

            return Ok(new { Message = "單一裝置登出成功。" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "單一裝置登出時發生異常。UserId: {UserId}, DeviceId: {DeviceId}", userId, deviceId);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = 500,
                Title = "伺服器內部錯誤",
                Detail = "登出程序執行失敗。"
            });
        }
    }

    /// <summary>
    /// 所有裝置登出（強制終止該使用者所有登入中的裝置工作階段）
    /// </summary>
    [HttpPost("logout-all")]
    [Authorize]
    [Menu("所有裝置登出", "fa-solid fa-power-off", order: 6, parent: "身份驗證")]
    [Description("強制終止該使用者所有裝置的有效 Token 與工作階段")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutAllAsync(CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ProblemDetails { Status = 401, Title = "未授權存取" });
        }

        try
        {
            // 將該使用者名下的所有 Active Sessions 全部撤銷
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
                Status = 500,
                Title = "伺服器內部錯誤",
                Detail = "全裝置登出程序執行失敗。"
            });
        }
    }
}