// 檔案路徑: Infrastructure/SGSFramework.AuthTokenBucket/Servers/TokenBucketEngine.cs

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Configurations;
using SGSFramework.AuthTokenBucket.Models;
using SGSFramework.Core.Abstractions.Entities.Base;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Logings;
using SGSFramework.Core.Abstractions.Models.Identities;
using SGSFramework.Core.Helpers;
using SGSFramework.Core.HttpAuditProviders;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace SGSFramework.AuthTokenBucket.Servers
{
    /// <summary>
    /// 安全防禦核心高併發水桶引擎
    /// </summary>
    /// <typeparam name="TUser">使用者實體類型</typeparam>
    public class TokenBucketEngine<TUser> where TUser : ApplicationUser, new()
    {
        private readonly ITokenStorageProvider _storageProvider;
        private readonly UserManager<TUser> _userManager;
        private readonly TokenManager _tokenManager;
        private readonly AuthTokenBucketOptions _options;
        private readonly ILogger<TokenBucketEngine<TUser>> _logger;
        private readonly ISecurityLogger _securityLogger;
        private readonly IAuditProvider _auditProvider;

        public TokenBucketEngine(
            ITokenStorageProvider storageProvider,
            UserManager<TUser> userManager,
            TokenManager tokenManager,
            IOptions<AuthTokenBucketOptions> options,
            ILogger<TokenBucketEngine<TUser>> logger,
            ISecurityLogger securityLogger,
            IAuditProvider auditProvider)
        {
            _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _securityLogger = securityLogger ?? throw new ArgumentNullException(nameof(securityLogger));
            _auditProvider = auditProvider ?? throw new ArgumentNullException(nameof(auditProvider));
        }

        public async Task<UserRefreshToken?> GetActiveSessionAsync(string userId, string deviceId)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(deviceId)) return null;
            return await _storageProvider.GetActiveSessionAsync(userId, deviceId);
        }

        public async Task<TokenResult> IssueInitialSessionAsync(TUser user, string deviceId, string deviceName, string clientIp)
        {
            ArgumentNullException.ThrowIfNull(user);

            string userIdString = user.Id.ToString(); // 轉為 string 供文字欄位與服務調用
            string rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            string tokenHash = HashHelper.ComputeHash(rawRefreshToken);
            DateTime expiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenExpirationDays);

            var permission = new BigBitmaskPermission(null);
            permission.SetPermission(5);
            permission.SetPermission(72);
            permission.SetPermission(130);

            string realJwtAccessToken = await _tokenManager.GenerateAccessTokenAsync(user, permission.ToString(), deviceId);

            var newSessionEntity = new UserRefreshToken
            {
                UserId = userIdString, // ✅ 轉為 string
                DeviceId = deviceId,
                DeviceName = deviceName,
                RefreshTokenHash = tokenHash,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                LastActiveAt = DateTime.UtcNow,
                ClientIp = clientIp,
                IsDead = false,
                IsFrozen = false
            };

            await _storageProvider.SaveInitialSessionAsync(newSessionEntity);
            await _storageProvider.EnforceMaxDeviceLimitAsync(userIdString, 5); // ✅ 轉為 string

            return new TokenResult
            {
                AccessToken = realJwtAccessToken,
                RefreshToken = rawRefreshToken,
                ExpiresAt = expiresAt
            };
        }

        /// <summary>
        /// 雙向權限票據高併發輪轉刷新
        /// </summary>
        public async Task<TokenResult> RefreshSessionAsync(TUser user, string deviceId, string oldRefreshToken)
        {
            ArgumentNullException.ThrowIfNull(user);

            string userIdString = user.Id.ToString(); // 轉為 string
            string oldHash = HashHelper.ComputeHash(oldRefreshToken);
            string newRawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            string newHash = HashHelper.ComputeHash(newRawToken);
            DateTime expiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenExpirationDays);

            var result = await _storageProvider.ValidateAndRotateTokenAsync(
                userIdString, deviceId, oldHash, newHash, expiresAt, _options.RefreshTokenGracePeriodSeconds); // ✅ 轉為 string

            if (result == null) throw new SecurityTokenException("ACCOUNT_FROZEN_OR_INVALID_SESSION");

            if (result.Status == RotationStatus.ReplayAttackDetected)
            {
                _logger.LogCritical("[Security-Alert] 偵測到 Token 惡意重放！用戶: {UserId}", userIdString);
                throw new SecurityTokenException("TOKEN_REPLAY_ATTACK_DETECTED");
            }

            var permission = new BigBitmaskPermission(null);
            permission.SetPermission(5);
            permission.SetPermission(72);

            string newJwtAccessToken = await _tokenManager.GenerateAccessTokenAsync(user, permission.ToString(), deviceId);

            _securityLogger.LogSecurity(
                eventCode: "SEC-200-TOKEN-REFRESH-SUCCESS",
                eventCategory: "Auth.TokenRefresh",
                userId: userIdString, // ✅ 轉為 string
                clientIp: _auditProvider.RemoteIp ?? "0.0.0.0",
                messageTemplate: "權杖交換成功，新權杖ID: {NewTokenId}, 舊權杖ID: {OldTokenId}",
                newRawToken,
                oldRefreshToken
            );

            return new TokenResult
            {
                AccessToken = newJwtAccessToken,
                RefreshToken = result.Status == RotationStatus.GracePeriodMatch ? oldRefreshToken : newRawToken,
                ExpiresAt = result.ExpiresAt
            };
        }

        public async Task<bool> EmergencyFreezeAsync(string userId, string reason)
        {
            if (string.IsNullOrWhiteSpace(userId)) return false;

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("全域緊急熔斷失敗：找不到指定用戶。用戶ID: {UserId}", userId);
                return false;
            }

            await _userManager.UpdateSecurityStampAsync(user);
            bool isFrozen = await _storageProvider.FreezeAndRevokeAllSessionsAsync(userId, reason);

            _logger.LogCritical("[Security-Event:SEC-911-LOCKDOWN] 已成功執行資安雙軌聯防熔斷。用戶: {UserId}, 原因: {Reason}", userId, reason);
            return isFrozen;
        }

        public async Task<bool> CompleteRemediationAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return false;

            bool isCleared = await _storageProvider.RemediateAndClearFrozenSessionsAsync(userId);
            _logger.LogInformation("[Security-Event:SEC-200-REMEDIATION] 用戶實名補償成功，已完成環境解凍與稽核清理。用戶: {UserId}", userId);
            return isCleared;
        }

        public async Task<bool> ExecuteGlobalLockdownAsync(string userId, string reason, string clientIp)
        {
            if (string.IsNullOrWhiteSpace(userId)) return false;

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("全域緊急熔斷失敗：找不到指定用戶。用戶ID: {UserId}", userId);
                return false;
            }

            await _userManager.UpdateSecurityStampAsync(user);
            bool isFrozen = await _storageProvider.FreezeAndRevokeAllSessionsAsync(userId, reason);

            using (Serilog.Context.LogContext.PushProperty("LogType", "Security"))
            using (Serilog.Context.LogContext.PushProperty("UserId", userId))
            using (Serilog.Context.LogContext.PushProperty("EventCategory", "Auth.GlobalLockdown"))
            using (Serilog.Context.LogContext.PushProperty("ClientIp", clientIp))
            {
                _logger.LogCritical("[Security-Event:SEC-911-LOCKDOWN] 已成功執行資安雙軌聯防熔斷。用戶: {UserId}, 原因: {Reason}, 來源IP: {ClientIp}", userId, reason, clientIp);
            }

            return isFrozen;
        }
    }
}