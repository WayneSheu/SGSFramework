using Microsoft.EntityFrameworkCore;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Identities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.AuthTokenBucket.Repositories;

public sealed class UserRefreshTokenRepository<TDbContext> : IUserRefreshTokenRepository
    where TDbContext : DbContext, ITokenDbContext
{
    private readonly TDbContext _context;

    public UserRefreshTokenRepository(TDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<int> GetActiveOnlineUserCountAsync(int activityWindowMinutes)
    {
        return await GetActiveOnlineUserCountAsync(activityWindowMinutes, default);
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


    /// <summary>
    /// 取得指定用戶的所有活躍會話
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<IEnumerable<UserRefreshToken>> GetActiveSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserRefreshTokens
            .Where(t => t.UserId == userId && !t.IsDead && !t.IsFrozen && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);
    }

    public async Task RevokeSessionAsync(string userId, string deviceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        try
        {
            var tokens = await _context.UserRefreshTokens
                .Where(t => t.UserId == userId && t.DeviceId == deviceId && !t.IsDead)
                .ToListAsync(cancellationToken);

            if (tokens.Count != 0)
            {
                foreach (var token in tokens)
                {
                    token.IsDead = true;
                }
                await _context.SaveChangesAsync(cancellationToken);
            }
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
            var tokens = await _context.UserRefreshTokens
                .Where(t => t.UserId == userId && !t.IsDead)
                .ToListAsync(cancellationToken);

            if (tokens.Count != 0)
            {
                foreach (var token in tokens)
                {
                    token.IsDead = true;
                }
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"撤銷使用者 {userId} 所有裝置的工作階段時發生異常。", ex);
        }
    }
}