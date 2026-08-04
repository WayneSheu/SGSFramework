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
using SGSFramework.Core.Abstractions.Logings;
using SGSFramework.Core.Abstractions.Menus;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Core.HttpAuditProviders;
using System.Security.Claims;

namespace SGSFramework.AuthTokenBucket.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [ApiExplorerSettings(GroupName = "Auth")]
    [Menu("身份驗證", "fa-solid fa-user-lock", order: 1, parent: "Auth")]
    public sealed class AuthController : ApiControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly TokenManager _tokenManager;
        private readonly ITokenStorageProvider _storageProvider;
        private readonly IUserRefreshTokenRepository _tokenRepository;
        private readonly TokenBucketEngine<IdentityUser> _tokenEngine;
        private readonly AuthTokenBucketOptions _options;
        private readonly ILogger<AuthController> _logger;
        private readonly IAuditProvider _auditProvider;
        private readonly ISecurityLogger _securityLogger;
        private readonly IDynamicMenuService _menuService;
        private readonly IUserRuntimeScopeService _runtimeScopeService;

        public AuthController( 
            UserManager<IdentityUser> userManager,
            TokenManager tokenManager,
            ITokenStorageProvider storageProvider,
            IUserRefreshTokenRepository tokenRepository,
            TokenBucketEngine<IdentityUser> tokenEngine,
            IOptions<AuthTokenBucketOptions> options,
            ILogger<AuthController> logger,
            IAuditProvider auditProvider,
            ISecurityLogger securityLogger,
            IDynamicMenuService menuService
            )     
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
            _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
            _tokenRepository = tokenRepository ?? throw new ArgumentNullException(nameof(tokenRepository));
            _tokenEngine = tokenEngine ?? throw new ArgumentNullException(nameof(tokenEngine));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));//一般Log
            _auditProvider = auditProvider ?? throw new ArgumentNullException(nameof(auditProvider));
            _securityLogger = securityLogger ?? throw new ArgumentNullException(nameof(securityLogger));//資安稽核Log
            _menuService=menuService?? throw new ArgumentNullException(nameof(menuService));
        }

        /// <summary>
        /// 標準帳密登入（全面整合大容量 Bitmask 權限與 TokenBucketEngine 基礎設施）
        /// 使用者登入成功後，取得 JWT Token 同時回傳該使用者的動態階層選單
        /// </summary>
        [HttpPost("login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var user = await _userManager.FindByNameAsync(request.Email) ?? await _userManager.FindByEmailAsync(request.Email);
                if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
                {
                    // 💡 優化後：採用強型別結構化日誌，並精準綁定識別碼與參數
                    _logger.LogWarning(
                        "[Security-Event:{EventCode}] 登入失敗：帳號或密碼錯誤。帳號: {TargetUser}, 來源IP: {RemoteIp}",
                        "SEC-401-Unauthorized", // {EventCode}: 用於快速檢索與 SIEM 告警分流機制
                        request.Email,          // {TargetUser}: 統一的資安追蹤標的欄位
                        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown" // {RemoteIp}: 補充關鍵審計上下文
                    );

                    return Unauthorized(new { message = "帳號或密碼錯誤。" });
                }
                
                string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "UNKNOWN-DEVICE";
                string deviceName = Request.Headers["User-Agent"].FirstOrDefault() ?? "Generic Browser";
                string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

                // 🚀 使用引擎統一簽發與分流（包含超過 64 位的大容量 Bitmask 權限包裝）
                var tokenResult = await _tokenEngine.IssueInitialSessionAsync(user, deviceId, deviceName, clientIp);

                _logger.LogInformation("使用者 {Email} 登入成功，裝置 ID: {DeviceId}", user.Email, deviceId);

                // 1. 驗證帳密並取得使用者宣告與對應權限字串集合 (例如從 Claims 或使用者角色中提取)
                var userPermissions = new List<string> { "System.Dashboard", "Module.Org.View" }; // 範例權限

                // 2. 根據使用者權限透過 DynamicMenuService 組合動態選單
                var menuTree = await _menuService.GetUserMenuAsync(userPermissions);

                // 3. 回傳登入 Token 與前端渲染所需的選單結構
                return Ok(new
                {
                    Menus = menuTree,
                    TokenData = tokenResult,
                    Message = "登入成功",
                });


                //return Ok(new
                //{
                //    Message = "登入成功",
                //    TokenData = tokenResult
                //});
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "標準帳密登入端點發生未預期核心異常。");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "伺服器內部錯誤", detail = ex.Message });
            }
        }

        /// <summary>
        /// 雙向權限票據高併發輪轉刷新（對齊更新後的 TokenBucketEngine 參數規範）
        /// </summary>
        [HttpPost("refresh")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                // 1. 解析過期的不安全記號以提取 User 身分
                var principal = _tokenManager.GetPrincipalFromExpiredToken(request.AccessToken);
                if (principal == null)
                {
                    return BadRequest(new { message = "無效的存取記號 (Invalid Access Token)。" });
                }

                string userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return Unauthorized(new { message = "使用者不存在或已被刪除。" });
                }

                string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "UNKNOWN-DEVICE";


                // 2. 呼叫引擎執行核心雙向換票
                var tokenResult = await _tokenEngine.RefreshSessionAsync(user, deviceId, request.RefreshToken);

                return Ok(tokenResult);
            }
            catch (Microsoft.IdentityModel.Tokens.SecurityTokenException ex) when (ex.Message == "TOKEN_REPLAY_ATTACK_DETECTED")
            {
                //_logger.LogCritical("偵測到 Token 重放攻擊熔斷！");
                
                // 結構化記錄安全稽核事件
                _securityLogger.LogSecurity(
                eventCode: "SEC-401-TOKEN_REPLAY_ATTACK_DETECTED",
                eventCategory: "Auth.TokenRefresh",
                userId: UserInfo.UserId ?? "Unknown",
                clientIp: UserInfo.ClientIp ?? "0.0.0.0",
                // 💡 建議：訊息範本中的具名參數，名稱儘量與傳入的變數名或後端資料庫欄位名「保持一致」
                // 將 {TargetUserId} 改為 {UserId}，將 {RemoteIp} 改為 {ClientIp}，這樣在 ELK 或資料庫搜尋時欄位會更統一。
                messageTemplate: "偵測到 Token 重放攻擊熔斷！用戶識別碼: {UserId}, 來源IP: {ClientIp}",
                UserInfo.UserId ?? "Unknown", // 💡 確保傳入參數與上方 Null 檢查邏輯一致
                UserInfo.ClientIp ?? "0.0.0.0"
                );

                return StatusCode(StatusCodes.Status401Unauthorized, new { message = "憑證異常，檢測到潛在的重放安全威脅，已強制終止所有連線。" });
            }
            catch (Microsoft.IdentityModel.Tokens.SecurityTokenException ex) when (ex.Message == "ACCOUNT_FROZEN_OR_INVALID_SESSION")
            {

                // 🟢 審計點：結構化記錄安全稽核事件，修正訊息範本（Message Template）命名並與後方參數完全對齊
                _securityLogger.LogSecurity(
                    eventCode: "SEC-401-INVALID-SESSION", // 💡 建議將 "SEC-425 Too Early" 規範化為相符的 401 狀態碼
                    eventCategory: "Auth.TokenRefresh",
                    userId: UserInfo.UserId ?? "Unknown",
                    clientIp: UserInfo.ClientIp ?? "0.0.0.0",
                    messageTemplate: "帳號已凍結或工作階段無效。用戶識別碼: {TargetUserId}, 來源IP: {RemoteIp}", // 💡 移除範本內的點號與表達式字串，改用強型別屬性名稱
                    UserInfo.UserId,
                    UserInfo.ClientIp
                );

            
                return StatusCode(StatusCodes.Status401Unauthorized, new { message = "工作階段已失效或帳戶已被鎖定。" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刷新 Token 時發生核心異常。");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "伺服器內部錯誤" });
            }
        }

        /// <summary>
        /// 獲取線上即時活動用戶數觀測
        /// </summary>
        [HttpGet("online-count")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetOnlineUserCount([FromQuery] int? windowMinutes = null)
        {
            int finalWindow = windowMinutes ?? (_options.AccessTokenExpirationMinutes + 2);

            if (finalWindow <= 0 || finalWindow > 1440)
            {
                return BadRequest(new { message = "活動觀測時間視窗必須介於 1 到 1440 分鐘之間。" });
            }

            try
            {
                int activeUsers = await _tokenRepository.GetActiveOnlineUserCountAsync(finalWindow);

                return Ok(new
                {
                    ActiveOnlineUsers = activeUsers,
                    ObservationWindowMinutes = finalWindow,
                    IsUsingDefaultConfigWindow = !windowMinutes.HasValue,
                    CalculatedAtUtc = DateTime.UtcNow
                });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "獲取線上人數統計時發生未知錯誤。");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "內部資料統計異常" });
            }
        }

        /// <summary>
        /// 高階主管切換作用中的實驗室上下文 (無感切換核心 API)
        /// </summary>
        [HttpPost("switch-context")]
        [ProducesResponseType(typeof(UserPermissionProfileDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> SwitchContext([FromBody] SwitchLabRequest request, CancellationToken cancellationToken)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // 呼叫 Service 驗證主管是否管轄該 TargetLabId，並獲取該實驗室的權限遮罩與選單
            var newProfile = await _runtimeScopeService.SwitchLaboratoryAsync(userId, request.TargetLabId, cancellationToken);

            if (newProfile is null)
            {
                return Forbid("您無權存取或切換至該實驗室");
            }

            return Ok(newProfile);
        }
    }


    /// <summary>
    /// 
    /// </summary>
    public sealed class RefreshTokenRequest
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    public sealed class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class SwitchLabRequest
    {
        public Guid TargetLabId { get; set; }
    }
}