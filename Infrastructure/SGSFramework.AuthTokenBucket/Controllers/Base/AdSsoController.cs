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
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.AuthTokenBucket.Servers;

namespace SGSFramework.AuthTokenBucket.Controllers.Base
{
    /// <summary>
    /// 現代雲端 AD (Microsoft Entra ID / OIDC SSO)
    /// </summary>
    /// <typeparam name="TUser"></typeparam>
    [ApiController]
    [Route("api/auth/sso")]
    public abstract class AdSsoController<TUser> : ApiControllerBase where TUser : IdentityUser, new()
    {
        private readonly TokenBucketEngine<TUser> _tokenEngine;
        private readonly UserManager<TUser> _userManager;
        private readonly IConfiguration _configuration;

        public AdSsoController(
            TokenBucketEngine<TUser> tokenEngine,
            UserManager<TUser> userManager,
            IConfiguration configuration)
        {
            _tokenEngine = tokenEngine;
            _userManager = userManager;
            _configuration = configuration;
        }

        /// <summary>
        /// 接收前端微軟 SSO 成功後的 OIDC 票據並實施防禦快取綁定
        /// </summary>
        [HttpPost("microsoft")]
        public async Task<IActionResult> MicrosoftSsoCallback([FromBody] MicrosoftSsoRequest request)
        {
            try
            {
                // 1. 驗證來自微軟 Entra ID 的 Identity Token 雜湊有效性
                var principal = await ValidateMicrosoftTokenAsync(request.IdToken);
                if (principal == null) return Unauthorized(new { message = "微軟身份憑證驗證無效。" });

                // 2. 從 Claims 中提取用戶在企業 AD 內的唯一標識符（UPN 或 Email）
                string userEmail = principal.FindFirst(ClaimTypes.Upn)?.Value
                                   ?? principal.FindFirst(ClaimTypes.Email)?.Value
                                   ?? string.Empty;

                if (string.IsNullOrWhiteSpace(userEmail)) return BadRequest(new { message = "無法從 AD 認證中提取有效的用戶識別碼。" });

                // 3. 確保該 AD 用戶已同步或存在於本系統的 Identity 體系中
                var user = await _userManager.FindByEmailAsync(userEmail);
                if (user == null)
                {
                    // 實務企業策略：若 AD 有此人但本地系統尚未建立，可在此實施「JIT 自動撥備（Just-In-Time Provisioning）」
                    user = new TUser { UserName = userEmail, Email = userEmail };
                    await _userManager.CreateAsync(user);
                }

                // 4. 提取硬體特徵 ID 綁定
                string deviceId = Request.Headers["X-Device-Id"].FirstOrDefault() ?? "SSO-UNKNOWN-DEVICE";

                // 5. 聯防啟動：交給套件引擎核發具備防高併發踩踏、重放防禦的 RefreshToken
                // 此處需要傳入初始化的隨機 RefreshToken 以便建立儲存列
                string initialRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

                // 利用我們套件既有的 RefreshSession 管道，將該設備註冊進入安全水桶中
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
            string tenantId = _configuration["AzureAd:TenantId"]!;
            string clientId = _configuration["AzureAd:ClientId"]!;
            string stsDiscoveryEndpoint = $"https://login.microsoftonline.com/{tenantId}/v2.0/.well-known/openid-configuration";

            var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(stsDiscoveryEndpoint, new OpenIdConnectConfigurationRetriever());
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

            try
            {
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
