namespace SGSFramework.AuthTokenBucket.Services;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
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
/// 安全防禦核心高併發水桶引擎 (優化重構版：策略模式解耦、支援完整 SuperAdmin 與全權限點宣告)
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

    public TokenBucketEngine(
        ITokenStorageProvider storageProvider,
        UserManager<TUser> userManager,
        ITokenManager tokenManager,
        IPermissionResolver permissionResolver,
        IOptions<AuthTokenBucketOptions> options,
        ILogger<TokenBucketEngine<TUser>> logger,
        ISecurityLogger securityLogger,
        IAuditProvider auditProvider)
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

            if (result.Status == RotationStatus.ReplayAttackDetected)
            {
                _logger.LogCritical("[Security-Alert] 偵測到 Token 惡意重放！用戶: {UserId}", userIdString);
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

            await _userManager.UpdateSecurityStampAsync(user);
            bool isFrozen = await _storageProvider.FreezeAndRevokeAllSessionsAsync(userId, reason);

            _logger.LogCritical("[Security-Event:SEC-911-LOCKDOWN] 已成功執行資安雙軌聯防熔斷。用戶: {UserId}, 原因: {Reason}", userId, reason);
            return isFrozen;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行緊急熔斷時發生未預期例外，UserId: {UserId}", userId);
            throw;
        }
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
            _logger.LogInformation("[Security-Event:SEC-200-REMEDIATION] 用戶實名補償成功，已完成環境解凍與稽核清理。用戶: {UserId}", userId);
            return isCleared;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行解凍補償時發生未預期例外，UserId: {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> ExecuteGlobalLockdownAsync(string userId, string reason, string clientIp)
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

            await _userManager.UpdateSecurityStampAsync(user);
            bool isFrozen = await _storageProvider.FreezeAndRevokeAllSessionsAsync(userId, reason);

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

            return isFrozen;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行全域鎖定時發生未預期例外，UserId: {UserId}", userId);
            throw;
        }
    }
}