using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Controllers.Base;
using SGSFramework.AuthTokenBucket.Models;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Identities;

namespace SGSFramework.Identity.Controllers.v1;

/// <summary>
/// 內部網路 Windows 網域無感單一登入控制器
/// </summary>
[ApiController]
[Route("api/v1/auth/sso/windows")]
[Produces("application/json")]
[ControllerTitle("Windows 網域單一登入", Icon = "fa-solid fa-windows", Order = 15, Description = "提供內部網路 Windows 網域無感單一登入 (AD SSO) 服務，整合大容量 Bitmask 權限與 TokenManager")]
public sealed class WindowsDomainAuthController(
    TokenBucketEngine<ApplicationUser> tokenEngine,
    UserManager<ApplicationUser> userManager,
    ILogger<WindowsDomainAuthController> logger)
    : WindowsAuthController<ApplicationUser>(tokenEngine, userManager)
{
    private readonly TokenBucketEngine<ApplicationUser> _tokenEngine = tokenEngine ?? throw new ArgumentNullException(nameof(tokenEngine));
    private readonly UserManager<ApplicationUser> _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    private readonly ILogger<WindowsDomainAuthController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// 地端 Windows 網域單一登入
    /// </summary>
    /// <returns>包含簽發 Token 與使用者資訊之登入結果</returns>
    [HttpGet("login")]
    [Authorize(AuthenticationSchemes = "Windows")]
    [Function("WindowsLogin", "Windows 網域單一登入", Icon = "fa-solid fa-lock", Order = 1, Description = "地端 Windows 網域無感單一登入端點，支援自動 JIT 撥備與 Token 簽發")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> WindowsLogin()
    {
        try
        {
            var userIdentity = HttpContext.User.Identity;
            if (userIdentity == null || !userIdentity.IsAuthenticated)
            {
                return Unauthorized(new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "網域認證失敗",
                    Detail = "未通過 Windows 網域身份認證。",
                    Instance = HttpContext.Request.Path
                });
            }

            string? domainAccount = userIdentity.Name;
            if (string.IsNullOrWhiteSpace(domainAccount))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "請求參數無效",
                    Detail = "無法解析有效的 Windows 網域帳號名稱。",
                    Instance = HttpContext.Request.Path
                });
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
                    _logger.LogError("JIT 自動撥備使用者失敗。帳號: {SsoUserName}, 錯誤原因: {Errors}",
                        ssoUserName, string.Join(", ", createResult.Errors.Select(e => e.Description)));

                    return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
                    {
                        Status = StatusCodes.Status500InternalServerError,
                        Title = "使用者撥備失敗",
                        Detail = "JIT 自動撥備使用者失敗。",
                        Instance = HttpContext.Request.Path
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
                // 若存在活體工作階段，直接調用引擎換票分流或重組真實憑證
                tokenResult = await _tokenEngine.RefreshSessionAsync(user, deviceId, existingSession.TokenHash);
            }
            else
            {
                // 執行初次建立：整合大容量 Bitmask 權限與 TokenManager 簽發實體 JWT 票據
                tokenResult = await _tokenEngine.IssueInitialSessionAsync(user, deviceId, deviceName, clientIp);
            }

            if (tokenResult == null || string.IsNullOrEmpty(tokenResult.AccessToken))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "資安防禦熔斷",
                    Detail = "認證水桶防禦熔斷：安全憑證簽發失敗。",
                    Instance = HttpContext.Request.Path
                });
            }

            _logger.LogInformation("Windows 網域單一登入成功。帳號: {DomainAccount}, 裝置 ID: {DeviceId}", domainAccount, deviceId);

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
            _logger.LogError(ex, "Windows 網域認證處理器發生核心異常。");
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "伺服器內部錯誤",
                Detail = "Windows 認證處理器發生核心異常。",
                Instance = HttpContext.Request.Path
            });
        }
    }
}