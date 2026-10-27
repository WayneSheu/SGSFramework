// ==========================================
// 檔案路徑: src/SGSFramework/Infrastructure/SGSFramework.AuthTokenBucket/Services/TokenManager.cs
// 架構層級: Infrastructure Layer / Services
// ==========================================

namespace SGSFramework.AuthTokenBucket.Services;

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Configurations;
using SGSFramework.Core.Abstractions.Identities;

public sealed class TokenManager : ITokenManager
{
    public const string DefaultKeyId = "SGS_AuthTokenBucket_SigningKey";
    private readonly ILogger<TokenManager> _logger;
    private readonly AuthTokenBucketOptions _options;
    private readonly SymmetricSecurityKey _signingKey;

    public TokenManager(
        ILogger<TokenManager> logger,
        IOptions<AuthTokenBucketOptions> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new InvalidOperationException("TokenManager 組態錯誤：SecretKey 不得為空。");
        }

        var keyBytes = Encoding.UTF8.GetBytes(_options.SecretKey);
        if (keyBytes.Length < 16)
        {
            throw new InvalidOperationException("TokenManager 資安防禦：SecretKey 長度必須至少 16 字元。");
        }

        _signingKey = new SymmetricSecurityKey(keyBytes)
        {
            KeyId = DefaultKeyId
        };
    }

    public string GenerateAccessToken(IdentityUser<Guid> user, string bitmaskString, string deviceId)
    {
        return GenerateAccessToken(user, bitmaskString, deviceId, null, false);
    }

    public string GenerateAccessToken(IdentityUser<Guid> user, string bitmaskString, string deviceId, IList<string>? roles, bool isSystemAdmin)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrEmpty(deviceId);

        try
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.UserName ?? string.Empty),
                new(SystemAccountClaims.AccountType, SystemAccountClaims.UserAccountType),
                new("device_id", deviceId),
                new("permissions", bitmaskString ?? string.Empty),
                new("perm_bits", bitmaskString ?? string.Empty), // 確保 Filter 讀取權限位元遮罩
                new("perm_mask", bitmaskString ?? string.Empty),
                new("is_admin", isSystemAdmin ? "true" : "false"), // 確保寫入管理員宣告
                new("IsSuperAdmin", isSystemAdmin ? "true" : "false"), // 確保寫入管理員宣告
                
                // 🚀 【資安聯防關鍵修復】將當前資料庫的安全戳記（SecurityStamp）包裹進 JWT Claims 中
                // 提供有無狀態 JWT 可被中介軟體隨時校驗的「時間戳快照」，以支援 0 秒全域強制熔斷
                new("AspNet.Identity.SecurityStamp", user.SecurityStamp ?? string.Empty)
            };

            if (roles != null)
            {
                foreach (var role in roles)
                {
                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        claims.Add(new Claim(ClaimTypes.Role, role));
                    }
                }
            }

            var signingCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

            var header = new JwtHeader(signingCredentials)
            {
                ["kid"] = DefaultKeyId
            };

            // 設定 Access Token 過期時間，若組態未設定或小於等於 0，則預設為 15 分鐘
            var expirationMinutes = _options.AccessTokenExpirationMinutes <= 0 ? 15 : _options.AccessTokenExpirationMinutes;

            var payload = new JwtPayload(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
                issuedAt: DateTime.UtcNow
            );

            var jwtToken = new JwtSecurityToken(header, payload);
            var tokenHandler = new JwtSecurityTokenHandler();

            return tokenHandler.WriteToken(jwtToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "簽發 JWT Access Token 時發生非預期例外，UserId: {UserId}", user.Id);
            throw;
        }
    }

    public SystemAccessTokenResult GenerateSystemAccessToken(SystemAccessTokenRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AccountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ClientId);

        try
        {
            var issuedAt = DateTimeOffset.UtcNow;

            // ClaimTypes.Name 使用帳號識別碼（sys_xxx），使稽核欄位 CreatedBy / UpdatedBy 自動記錄為系統帳號
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, request.AccountId),
                new(ClaimTypes.Name, request.AccountId),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new(SystemAccountClaims.AccountType, SystemAccountClaims.SystemAccountType),
                new(SystemAccountClaims.ClientId, request.ClientId),
                new(SystemAccountClaims.DisplayName, request.DisplayName ?? string.Empty),
                new(SystemAccountClaims.IssuedAtMilliseconds, issuedAt.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new("is_admin", "false"),
                new("IsSuperAdmin", "false")
            };

            foreach (var scope in request.Scopes)
            {
                claims.Add(new Claim(SystemAccountClaims.Scope, scope));
            }

            var signingCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
            var header = new JwtHeader(signingCredentials)
            {
                ["kid"] = DefaultKeyId
            };

            // JWT 時間欄位為秒精度；撤銷比對改用 iat_ms 宣告
            var now = DateTimeOffset.FromUnixTimeSeconds(issuedAt.ToUnixTimeSeconds());
            var lifetime = request.Lifetime <= TimeSpan.Zero ? TimeSpan.FromMinutes(15) : request.Lifetime;
            var expires = now.Add(lifetime);

            var payload = new JwtPayload(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: now.UtcDateTime,
                expires: expires.UtcDateTime,
                issuedAt: now.UtcDateTime
            );

            var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
            return new SystemAccessTokenResult(token, issuedAt, expires);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "簽發系統帳號 Access Token 時發生非預期例外，AccountId: {AccountId}", request.AccountId);
            throw;
        }
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token, string? clientIp = null)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrWhiteSpace(_options.Issuer),
            ValidIssuer = _options.Issuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(_options.Audience),
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            IssuerSigningKeyResolver = (t, st, kid, vp) => new SecurityKey[] { _signingKey },
            ValidateLifetime = false,
            ClockSkew = TimeSpan.FromSeconds(_options.ClockSkewSeconds < 0 ? 0 : _options.ClockSkewSeconds)
        };

        var tokenHandler = new JwtSecurityTokenHandler();

        try
        {
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);

            if (securityToken is not JwtSecurityToken jwtSecurityToken ||
                !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
            {
                LogSecurityAudit("Auth.TokenDowngradeAttack", "SEC-401-INVALID-ALGORITHM", "偵測到未授權的 Token 演算法降級嘗試！", clientIp, null);
                return null;
            }

            return principal;
        }
        catch (Exception ex)
        {
            LogSecurityAudit("Auth.MalformedToken", "SEC-401-TOKEN-SIGNATURE-MALFORMED", "傳入的過期 Token 簽章驗證失敗或疑似偽造憑證。", clientIp, ex);
            return null;
        }
    }

    private void LogSecurityAudit(string eventCategory, string errorCode, string message, string? clientIp, Exception? ex)
    {
        var ipAddress = string.IsNullOrWhiteSpace(clientIp) ? "UNKNOWN" : clientIp;

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["LogType"] = "Security",
            ["EventCategory"] = eventCategory,
            ["ClientIp"] = ipAddress
        }))
        {
            if (ex != null)
            {
                _logger.LogError(ex, "[Security-Audit:{ErrorCode}] {Message} 來源IP: {ClientIp}", errorCode, message, ipAddress);
            }
            else
            {
                _logger.LogCritical("[Security-Audit:{ErrorCode}] {Message} 來源IP: {ClientIp}", errorCode, message, ipAddress);
            }
        }
    }
}