using Microsoft.EntityFrameworkCore;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Entities.Identities;

namespace SGSFramework.AuthTokenBucket.Repositories;

/// <summary>
/// 使用者刷新權杖資料存取倉儲
/// </summary>
/// <typeparam name="TDbContext"></typeparam>
public sealed class UserRefreshTokenRepository<TDbContext> : IUserRefreshTokenRepository
    where TDbContext : DbContext, ITokenDbContext
{
    private readonly TDbContext _context;
    private readonly ISessionValidator _sessionValidator;
    private readonly ISessionNotifier _sessionNotifier;

    public UserRefreshTokenRepository(TDbContext context, ISessionValidator sessionValidator, ISessionNotifier sessionNotifier)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _sessionValidator = sessionValidator ?? throw new ArgumentNullException(nameof(sessionValidator));
        _sessionNotifier = sessionNotifier ?? throw new ArgumentNullException(nameof(sessionNotifier));
    }

    /// <summary>
    /// 依據活動觀測視窗，計算目前系統中的活躍在線不重複人數
    /// </summary>
    /// <param name="activityWindowMinutes"></param>
    /// <returns></returns>
    public Task<int> GetActiveOnlineUserCountAsync(int activityWindowMinutes)
    {
        return GetActiveOnlineUserCountAsync(activityWindowMinutes, CancellationToken.None);
    }

    public async Task<int> GetActiveOnlineUserCountAsync(int activityWindowMinutes, CancellationToken cancellationToken)
    {
        try
        {
            var utcNow = DateTime.UtcNow;
            var thresholdTime = utcNow.AddMinutes(-activityWindowMinutes);

            return await _context.UserRefreshTokens
                .AsNoTracking()
                .Where(token => !token.IsDead && !token.IsFrozen && token.ExpiresAt > utcNow && token.LastActiveAt >= thresholdTime)
                .Select(token => token.UserId)
                .Distinct()
                .CountAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException("執行泛型在線人數統計查詢時發生資料庫核心異常。", ex);
        }
    }

    public async Task<IEnumerable<UserRefreshToken>> GetActiveSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        try
        {
            return await _context.UserRefreshTokens
                .AsNoTracking()
                .Where(t => t.UserId == userId && !t.IsDead && !t.IsFrozen && t.ExpiresAt > DateTime.UtcNow)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"獲取使用者 {userId} 的活躍工作階段時發生資料庫異常。", ex);
        }
    }

    public async Task RevokeSessionAsync(string userId, string deviceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        try
        {
            // 使用 EF Core ExecuteUpdateAsync 直接發送 UPDATE SQL，減少記憶體負擔並增加吞吐量
            await _context.UserRefreshTokens
                .Where(t => t.UserId == userId && t.DeviceId == deviceId && !t.IsDead)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsDead, true), cancellationToken);

            // 立即清除驗證快取，使該裝置尚未過期的 Access Token 於下一次請求即失效
            _sessionValidator.InvalidateDevice(userId, deviceId);

            // 即時推播：同一裝置的其他分頁立即登出
            await _sessionNotifier.NotifyDeviceRevokedAsync(userId, deviceId, "logout", cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"撤銷使用者 {userId} 在裝置 {deviceId} 的工作階段時發生異常。", ex);
        }
    }

    public async Task RevokeAllUserSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        try
        {
            await _context.UserRefreshTokens
                .Where(t => t.UserId == userId && !t.IsDead)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsDead, true), cancellationToken);

            // 立即清除該使用者所有裝置的驗證快取，使其他裝置於下一次請求即被登出
            _sessionValidator.InvalidateUser(userId);

            // 即時推播：該使用者所有已連線裝置立即登出
            await _sessionNotifier.NotifyUserRevokedAsync(userId, "logout-all", cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"撤銷使用者 {userId} 所有裝置的工作階段時發生異常。", ex);
        }
    }
}