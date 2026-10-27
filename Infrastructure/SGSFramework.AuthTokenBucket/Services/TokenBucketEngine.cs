namespace SGSFramework.AuthTokenBucket.Services;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory; // 🚀 引入單機記憶體快取管理
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Configurations;
using SGSFramework.AuthTokenBucket.Models;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Logings;
using SGSFramework.Core.Helpers;
using SGSFramework.Core.HttpAuditProviders;

/// <summary>
/// 安全防禦核心高併發水桶引擎 (優化重構版：策略模式解耦、支援完整 SuperAdmin 與全權限點宣告、IMemoryCache 雙軌聯防熔斷)
/// </summary>
/// <typeparam name="TUser">使用者實體類型</typeparam>
public class TokenBucketEngine<TUser> where TUser : ApplicationUser, new()
{
    private readonly ITokenStorageProvider _storageProvider;
    private readonly UserManager<TUser> _userManager;
    private readonly ITokenManager _tokenManager;
    private readonly IPermissionResolver _permissionResolver;
    private readonly AuthTokenBucketOptions _options;
    private readonly ILogger<TokenBucketEngine<TUser>> _logger;
    private readonly ISecurityLogger _securityLogger;
    private readonly IAuditProvider _auditProvider;
    private readonly IMemoryCache _memoryCache; // 🚀 注入記憶體快取提供者
    private readonly TimeProvider _timeProvider;

    public TokenBucketEngine(
        ITokenStorageProvider storageProvider,
        UserManager<TUser> userManager,
        ITokenManager tokenManager,
        IPermissionResolver permissionResolver,
        IOptions<AuthTokenBucketOptions> options,
        ILogger<TokenBucketEngine<TUser>> logger,
        ISecurityLogger securityLogger,
        IAuditProvider auditProvider,
        IMemoryCache memoryCache, // 🚀 建構子注入 IMemoryCache
        TimeProvider timeProvider)
    {
        _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
        _permissionResolver = permissionResolver ?? throw new ArgumentNullException(nameof(permissionResolver));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _securityLogger = securityLogger ?? throw new ArgumentNullException(nameof(securityLogger));
        _auditProvider = auditProvider ?? throw new ArgumentNullException(nameof(auditProvider));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// 記錄使用者最後登入時間（帳密 / 2FA / SSO 登入成功時呼叫；Token 輪轉刷新不視為登入）。
    /// 以單一 UPDATE 直接寫入 LastLoginAt，不經 UserManager.UpdateAsync，
    /// 避免使用者驗證規則或 ConcurrencyStamp 輪替造成寫入失敗；寫入失敗僅記錄警告，不影響登入流程。
    /// </summary>
    public async Task RecordLoginAsync(TUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            var loginAt = _timeProvider.GetUtcNow();
            var userId = user.Id;

            var affected = await _userManager.Users
                .Where(u => u.Id == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, loginAt));

            // 同步記憶體中的實體，避免同一請求後續儲存時以舊值覆寫
            user.LastLoginAt = loginAt;

            if (affected == 0)
            {
                _logger.LogWarning("更新最後登入時間未影響任何資料列，UserId: {UserId}", userId);
            }
            else
            {
                _logger.LogInformation("已記錄使用者最後登入時間，UserId: {UserId}, LastLoginAt: {LastLoginAt}", userId, loginAt);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "更新最後登入時間時發生例外，UserId: {UserId}", user.Id);
        }
    }

    public async Task<UserRefreshToken?> GetActiveSessionAsync(string userId, string deviceId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        return await _storageProvider.GetActiveSessionAsync(userId, deviceId);
    }

    /// <summary>
    /// 初始化工作階段並簽發含完整角色與權限 Claims 的 Access Token
    /// </summary>
    public async Task<TokenResult> IssueInitialSessionAsync(TUser user, string deviceId, string deviceName, string clientIp)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrEmpty(deviceId);

        try
        {
            string userIdString = user.Id.ToString();
            string rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            string tokenHash = HashHelper.ComputeHash(rawRefreshToken);
            DateTime expiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenExpirationDays);

            // 1. 透過策略服務解析使用者動態權限與角色
            var roles = await _userManager.GetRolesAsync(user);
            var (permissionMask, isAdmin) = await _permissionResolver.ResolveUserPermissionsAsync(user, _userManager);

            // 2. 安全簽發 JWT Access Token
            string realJwtAccessToken = _tokenManager.GenerateAccessToken(user, permissionMask, deviceId, roles, isAdmin);

            // 3. 建立並持久化 Session 實體
            var newSessionEntity = new UserRefreshToken
            {
                UserId = userIdString,
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
            await _storageProvider.EnforceMaxDeviceLimitAsync(userIdString, _options.MaxDeviceCount);

            // 4. 建立新工作階段即代表登入成功，記錄最後登入時間
            await RecordLoginAsync(user);

            return new TokenResult
            {
                AccessToken = realJwtAccessToken,
                RefreshToken = rawRefreshToken,
                ExpiresAt = expiresAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "建立初始 Session 時發生例外，UserId: {UserId}, DeviceId: {DeviceId}", user.Id, deviceId);
            throw;
        }
    }

    /// <summary>
    /// 雙向權限票據高併發輪轉刷新
    /// </summary>
    public async Task<TokenResult> RefreshSessionAsync(TUser user, string deviceId, string oldRefreshToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrEmpty(deviceId);

        try
        {
            string userIdString = user.Id.ToString();
            string oldHash = HashHelper.ComputeHash(oldRefreshToken);
            string newRawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            string newHash = HashHelper.ComputeHash(newRawToken);
            DateTime expiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenExpirationDays);

            var result = await _storageProvider.ValidateAndRotateTokenAsync(
                userIdString, deviceId, oldHash, newHash, expiresAt, _options.RefreshTokenGracePeriodSeconds);

            if (result == null)
            {
                throw new SecurityTokenException("ACCOUNT_FROZEN_OR_INVALID_SESSION");
            }

            // 🚀 【資安聯防升級】一旦確認遭受重放攻擊，立即對該用戶實施全域自動聯防熔斷鎖定
            if (result.Status == RotationStatus.ReplayAttackDetected)
            {
                _logger.LogCritical("[Security-Alert:SEC-EMERGENCY-REPLAY] 偵測到 Token 惡意重放！啟動資安聯防全域阻斷。用戶: {UserId}", userIdString);
                await ExecuteGlobalLockdownAsync(userIdString, "系統自動熔斷：偵測到 Refresh Token 遭惡意重放。", _auditProvider.RemoteIp ?? "0.0.0.0");
                throw new SecurityTokenException("TOKEN_REPLAY_ATTACK_DETECTED");
            }

            // 透過策略服務解析最新權限 (確保權限變更即時生效)
            var roles = await _userManager.GetRolesAsync(user);
            var (permissionMask, isAdmin) = await _permissionResolver.ResolveUserPermissionsAsync(user, _userManager);

            string newJwtAccessToken = _tokenManager.GenerateAccessToken(user, permissionMask, deviceId, roles, isAdmin);

            _securityLogger.LogSecurity(
                eventCode: "SEC-200-TOKEN-REFRESH-SUCCESS",
                eventCategory: "Auth.TokenRefresh",
                userId: userIdString,
                clientIp: _auditProvider.RemoteIp ?? "0.0.0.0",
                messageTemplate: "權杖交換成功，用戶ID: {UserId}, 裝置ID: {DeviceId}",
                userIdString,
                deviceId
            );

            return new TokenResult
            {
                AccessToken = newJwtAccessToken,
                RefreshToken = result.Status == RotationStatus.GracePeriodMatch ? oldRefreshToken : newRawToken,
                ExpiresAt = result.ExpiresAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "刷新 Session 權杖時發生例外，UserId: {UserId}, DeviceId: {DeviceId}", user.Id, deviceId);
            throw;
        }
    }

    public async Task<bool> EmergencyFreezeAsync(string userId, string reason)
    {
        // 管理性 / 使用者主動操作（刪除帳號、變更密碼、狀態變更、手動緊急熔斷）：屬預期流程，不是攻擊
        return await CoreLockdownInternalAsync(userId, reason, null, isAttack: false);
    }

    public async Task<bool> ExecuteGlobalLockdownAsync(string userId, string reason, string clientIp)
    {
        // 偵測到攻擊（如 Refresh Token 重放）的自動熔斷：維持 Critical
        return await CoreLockdownInternalAsync(userId, reason, clientIp, isAttack: true);
    }

    public async Task<bool> CompleteRemediationAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        try
        {
            bool isCleared = await _storageProvider.RemediateAndClearFrozenSessionsAsync(userId);

            // 🚀 同步清理可能殘留的中介快取快照與權限快取
            _memoryCache.Remove($"db-stamp:{userId}");
            await _permissionResolver.EvictUserCacheAsync(userId);

            _logger.LogInformation("[Security-Event:SEC-200-REMEDIATION] 用戶實名補償成功，已完成環境解凍與稽核清理。用戶: {UserId}", userId);
            return isCleared;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行解凍補償時發生未預期例外，UserId: {UserId}", userId);
            throw;
        }
    }

    /// <summary>
    /// 🚀 核心熔斷私有整合方法（解耦多個 Lockdown 進入點，同步抹除 IMemoryCache 消除時間差漏洞）
    /// </summary>
    private async Task<bool> CoreLockdownInternalAsync(string userId, string reason, string? clientIp, bool isAttack)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                _logger.LogWarning("全域緊急熔斷失敗：找不到指定用戶。用戶ID: {UserId}", userId);
                return false;
            }

            // 1. 更新 Identity 安全戳記，使發行在外的 JWT 在下次中介軟體校驗時立即死卡
            await _userManager.UpdateSecurityStampAsync(user);

            // 2. 持久層全會話全面登出封鎖
            bool isFrozen = await _storageProvider.FreezeAndRevokeAllSessionsAsync(userId, reason);

            // 3. 🚀 資安最高規格補強：主動抹除單機 IMemoryCache，縮短 45 秒時間差死角
            _memoryCache.Remove($"db-stamp:{userId}");
            await _permissionResolver.EvictUserCacheAsync(userId);

            if (isAttack)
            {
                // 攻擊自動熔斷：Critical，會進入告警通道
                if (!string.IsNullOrEmpty(clientIp))
                {
                    using (_logger.BeginScope(new Dictionary<string, object>
                    {
                        ["LogType"] = "Security",
                        ["UserId"] = userId,
                        ["EventCategory"] = "Auth.GlobalLockdown",
                        ["ClientIp"] = clientIp
                    }))
                    {
                        _logger.LogCritical("[Security-Event:SEC-911-LOCKDOWN] 已成功執行資安雙軌聯防熔斷。用戶: {UserId}, 原因: {Reason}, 來源IP: {ClientIp}", userId, reason, clientIp);
                    }
                }
                else
                {
                    _logger.LogCritical("[Security-Event:SEC-911-LOCKDOWN] 已成功執行資安雙軌聯防熔斷。用戶: {UserId}, 原因: {Reason}", userId, reason);
                }
            }
            else
            {
                // 例行性作廢憑證：Information，不觸發告警（避免每次刪除帳號 / 變更密碼都發出 Fatal 告警）
                _logger.LogInformation("[Security-Event:SEC-201-SESSIONS-REVOKED] 已作廢使用者全部憑證與工作階段。用戶: {UserId}, 原因: {Reason}", userId, reason);
            }

            return isFrozen;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行全域鎖定熔斷時發生未預期例外，UserId: {UserId}", userId);
            throw;
        }
    }
}