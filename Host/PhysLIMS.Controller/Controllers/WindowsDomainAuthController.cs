using Azure.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.AuthTokenBucket.Models;
using SGSFramework.AuthTokenBucket.Servers;
using SGSFramework.AuthTokenBucket.Controllers.Base;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Core.Abstractions.Entities.Identities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SGSFramework.ApiInfrastructure.Controllers
{
    /// <summary>
    /// 內部網路 Windows 網域無感單一登入端點（全面整合 TokenManager 與大容量 Bitmask 最終對齊版）
    /// </summary>
    [ApiController]
    [Route("api/auth/sso/windows")]
    [ApiExplorerSettings(GroupName = "Auth")]
    public sealed class WindowsDomainAuthController : WindowsAuthController<IdentityUser>
    {
        private readonly TokenBucketEngine<IdentityUser> _tokenEngine;
        private readonly UserManager<IdentityUser> _userManager;

        public WindowsDomainAuthController(
            TokenBucketEngine<IdentityUser> tokenEngine,
            UserManager<IdentityUser> userManager)
            : base(tokenEngine, userManager)
        {
            _tokenEngine = tokenEngine ?? throw new ArgumentNullException(nameof(tokenEngine));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }

        /// <summary>
        /// 地端 Windows 網域單一登入
        /// </summary>
        [HttpGet("login")]
        [Authorize(AuthenticationSchemes = "Windows")]
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
                    user = new IdentityUser
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

                UserRefreshToken? existingSession = await _tokenEngine.GetActiveSessionAsync(user.Id, deviceId);
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
    }
}