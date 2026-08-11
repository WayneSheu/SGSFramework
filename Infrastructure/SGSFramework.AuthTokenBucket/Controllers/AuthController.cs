using System.ComponentModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Configurations;
using SGSFramework.AuthTokenBucket.DTOs;
using SGSFramework.AuthTokenBucket.Servers;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Logings;
using SGSFramework.Core.Abstractions.Menus;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Core.DTOs;
using SGSFramework.Core.HttpAuditProviders;

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

            string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "UNKNOWN-DEVICE";
            string deviceName = Request.Headers["User-Agent"].FirstOrDefault() ?? "Generic Browser";
            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

            var tokenResult = await _tokenEngine.IssueInitialSessionAsync(user, deviceId, deviceName, clientIp);

            _logger.LogInformation("使用者 {Email} 登入成功，裝置 ID: {DeviceId}", user.Email, deviceId);
            
            // 1. 從 UserRuntimeScopeService 取得實質權限清單
            IEnumerable<string> userPermissions = await _runtimeScopeService.GetUserPermissionsAsync(
                user.Id.ToString(),
                cancellationToken: cancellationToken);

            // 2. 傳入權限集合，算出一套帶有 Level 與 IsDisplay 控制標記的階層選單
            var menuTree = await _menuService.GetUserMenuAsync(userPermissions);

            return Ok(new LoginResponseDto
            {
                Menus = menuTree,
                TokenData = tokenResult,
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
    public async Task<IActionResult> SwitchContextAsync(
        [FromBody] SwitchLabRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "未授權存取",
                Detail = "無法識別目前的登入使用者身分。",
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
                    Title = "存取權限不足",
                    Detail = "您無權存取或切換至該實驗室。",
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
}