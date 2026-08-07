using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.AuthTokenBucket.Servers;
using SGSFramework.Core.Abstractions.Entities.Identities;

namespace SGSFramework.AuthTokenBucket.Controllers.Base
{
    /// <summary>
    /// 現代雲端 AD (Microsoft Entra ID / OIDC SSO) 泛型基礎控制器
    /// </summary>
    /// <typeparam name="TUser">使用者實體型態，必須繼承自 ApplicationUser 且具備無參數建構子</typeparam>
    [ApiController]
    [Route("api/auth/sso")]
    public abstract class AdSsoController<TUser> : ApiControllerBase
        where TUser : ApplicationUser, new() // <-- 將條件約束改為 ApplicationUser
    {
        private readonly TokenBucketEngine<TUser> _tokenEngine;
        private readonly UserManager<TUser> _userManager;
        private readonly IConfiguration _configuration;

        protected AdSsoController(
            TokenBucketEngine<TUser> tokenEngine,
            UserManager<TUser> userManager,
            IConfiguration configuration)
        {
            _tokenEngine = tokenEngine ?? throw new ArgumentNullException(nameof(tokenEngine));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// 接收前端微軟 SSO 成功後的 OIDC 票據並實施防禦快取綁定
        /// </summary>
        [HttpPost("microsoft")]
        public async Task<IActionResult> MicrosoftSsoCallback([FromBody] MicrosoftSsoRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.IdToken))
            {
                return BadRequest(new { message = "請求內容或 IdToken 無法為空。" });
            }

            try
            {
                // 1. 驗證來自微軟 Entra ID 的 Identity Token 雜湊有效性
                var principal = await ValidateMicrosoftTokenAsync(request.IdToken);
                if (principal == null)
                {
                    return Unauthorized(new { message = "微軟身份憑證驗證無效。" });
                }

                // 2. 從 Claims 中提取用戶在企業 AD 內的唯一標識符（UPN 或 Email）
                string userEmail = principal.FindFirst(ClaimTypes.Upn)?.Value
                                   ?? principal.FindFirst(ClaimTypes.Email)?.Value
                                   ?? string.Empty;

                if (string.IsNullOrWhiteSpace(userEmail))
                {
                    return BadRequest(new { message = "無法從 AD 認證中提取有效的用戶識別碼。" });
                }

                // 3. 確保該 AD 用戶已同步或存在於本系統的 Identity 體系中
                var user = await _userManager.FindByEmailAsync(userEmail);
                if (user == null)
                {
                    // 實務企業策略：JIT (Just-In-Time) 自動撥備建立用戶
                    user = new TUser
                    {
                        UserName = userEmail,
                        Email = userEmail,
                        FullName = principal.FindFirst(ClaimTypes.Name)?.Value ?? userEmail, // 已可正常讀取
                        CreatedAt = DateTimeOffset.UtcNow                   // 已可正常讀取
                    };

                    var createResult = await _userManager.CreateAsync(user);
                    if (!createResult.Succeeded)
                    {
                        var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                        return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"自動建立 SSO 用戶失敗: {errors}" });
                    }
                }

                // 4. 提取硬體特徵 ID 綁定
                string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "SSO-UNKNOWN-DEVICE";

                // 5. 產生初始安全亂數 RefreshToken 憑證
                string initialRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

                // 6. 呼叫令牌引擎核發 RefreshSession
                var tokenResult = await _tokenEngine.RefreshSessionAsync(user, deviceId, initialRefreshToken);

                return Ok(new
                {
                    Message = "AD 單一登入成功",
                    UserId = user.Id,
                    Email = user.Email,
                    TokenData = tokenResult
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = $"SSO 整合器發生核心異常: {ex.Message}" });
            }
        }

        private async Task<ClaimsPrincipal?> ValidateMicrosoftTokenAsync(string token)
        {
            string tenantId = _configuration["AzureAd:TenantId"] ?? string.Empty;
            string clientId = _configuration["AzureAd:ClientId"] ?? string.Empty;

            if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId))
            {
                return null;
            }

            string stsDiscoveryEndpoint = $"https://login.microsoftonline.com/{tenantId}/v2.0/.well-known/openid-configuration";

            try
            {
                var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    stsDiscoveryEndpoint,
                    new OpenIdConnectConfigurationRetriever());

                var openIdConfig = await configurationManager.GetConfigurationAsync();

                var validationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidAudience = clientId,
                    ValidateIssuer = true,
                    ValidIssuer = openIdConfig.Issuer,
                    IssuerSigningKeys = openIdConfig.SigningKeys,
                    ValidateLifetime = true
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
                return principal;
            }
            catch
            {
                return null;
            }
        }
    }

    public sealed class MicrosoftSsoRequest
    {
        public string IdToken { get; set; } = string.Empty;
    }
}