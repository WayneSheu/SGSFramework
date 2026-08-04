using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SGSFramework.AuthTokenBucket.Servers
{

    /// <summary>
    /// 專案內負責實體生長、簽發與校驗 JwtSecurityToken 的核心認證管理器
    /// </summary>
    public sealed class TokenManager
    {
        private readonly ILogger<TokenManager> _logger;
        // 實務開發請對齊您的組態檔金鑰設定
        private readonly byte[] _secretKey = Encoding.ASCII.GetBytes("SES_Core_Architecture_Secure_Token_Secret_Key_2026_Enterprise");
       
        public TokenManager(ILogger<TokenManager> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 配合大容量 Bitmask 遮罩生成真實合法的 JWT 記號字串
        /// </summary>
        public Task<string> GenerateAccessTokenAsync(IdentityUser user, string bitmaskString, string deviceId)
        {
            ArgumentNullException.ThrowIfNull(user);

            var tokenHandler = new JwtSecurityTokenHandler();

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName ?? string.Empty),
                new("device_id", deviceId),
                // 🚀 核心設計：將超過 64 位的複合權限遮罩以字串型式注入 Claim 載體
                new("permissions", bitmaskString)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(2), // AccessToken 預設生命週期
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(_secretKey),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return Task.FromResult(tokenHandler.WriteToken(token));
        }

        /// <summary>
        /// 🚀 補回核心實作：從已過期或不安全的 Access Token 中安全提取 ClaimsPrincipal 身分
        /// </summary>
        /// <param name="token">傳入需要刷新的 Access Token</param>
        /// <returns>解析成功返回 ClaimsPrincipal，驗證失敗則返回 null</returns>
        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = false,
                ValidateIssuer = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_secretKey),
                // 🚀 關鍵資安實務：必須關閉存活期檢查
                // 否則已過期的 Token 會直接被驗證器拋出安全例外，導致無法提取身分執行後續的 Refresh 換票輪轉
                ValidateLifetime = false
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

                // 🚀 防禦性安全檢查：確保記號的加密演算法與我們簽發的強強度簽章一致（防禦 Alg=None 的記號降級篡改漏洞）
                if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    return null;
                }

                return principal;
            }
            catch (Exception)
            {
                // 當簽章無效、格式崩潰或遭受惡意篡改時，直接返回 null 進行阻斷
                return null;
            }
        }


        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token, string clientIp)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;

            var tokenValidationParameters = new TokenValidationParameters { /* 參數保持不變 */ };
            var tokenHandler = new JwtSecurityTokenHandler();

            try
            {
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

                // 🚀 防禦性安全檢查：確保演算法一致性
                if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                    !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    // 💡 修正：演算法降級篡改稽核點
                    using (Serilog.Context.LogContext.PushProperty("LogType", "Security"))
                    using (Serilog.Context.LogContext.PushProperty("EventCategory", "Auth.TokenDowngradeAttack"))
                    using (Serilog.Context.LogContext.PushProperty("ClientIp", clientIp))
                    {
                        _logger.LogCritical("[Security-Audit:SEC-401-INVALID-ALGORITHM] 偵測到未授權的 Token 演算法降級嘗試！來源IP: {ClientIp}", clientIp);
                    }
                    return null;
                }

                return principal;
            }
            catch (Exception ex)
            {
                // 💡 修正：簽章無效或遭受惡意篡改稽核點
                using (Serilog.Context.LogContext.PushProperty("LogType", "Security"))
                using (Serilog.Context.LogContext.PushProperty("EventCategory", "Auth.MalformedToken"))
                using (Serilog.Context.LogContext.PushProperty("ClientIp", clientIp))
                {
                    _logger.LogError(ex, "[Security-Audit:SEC-401-TOKEN-SIGNATURE-MALFORMED] 傳入的過期 Token 簽章驗證失敗，疑似偽造憑證。來源IP: {ClientIp}", clientIp);
                }
                return null;
            }
        }

    }
}