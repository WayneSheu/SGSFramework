using Microsoft.EntityFrameworkCore;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.DbContexts;
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

    public UserRefreshTokenRepository(TDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
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
                .Where(token => token.ExpiresAt > utcNow && token.LastActiveAt >= thresholdTime)
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
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"撤銷使用者 {userId} 所有裝置的工作階段時發生異常。", ex);
        }
    }
}