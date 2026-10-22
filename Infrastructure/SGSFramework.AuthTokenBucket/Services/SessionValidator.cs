using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.DbContexts;

namespace SGSFramework.AuthTokenBucket.Services;

/// <summary>
/// 以短期記憶體快取降低每次請求的資料庫查詢量。
/// 僅快取「有效」結果；撤銷時以使用者版本號讓該使用者所有裝置快取同時失效。
/// 未經本服務的撤銷路徑（例如裝置數上限淘汰）最晚於 <see cref="PositiveCacheDuration"/> 後生效。
/// </summary>
public sealed class SessionValidator<TDbContext> : ISessionValidator
    where TDbContext : DbContext, ITokenDbContext
{
    private static readonly TimeSpan PositiveCacheDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan VersionCacheDuration = TimeSpan.FromHours(1);

    private readonly TDbContext _context;
    private readonly IMemoryCache _cache;

    public SessionValidator(TDbContext context, IMemoryCache cache)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<bool> IsSessionActiveAsync(string userId, string deviceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        var key = DeviceKey(userId, deviceId);
        if (_cache.TryGetValue(key, out bool cached) && cached)
        {
            return true;
        }

        var isActive = await _context.UserRefreshTokens
            .AsNoTracking()
            .AnyAsync(t => t.UserId == userId && t.DeviceId == deviceId && !t.IsDead && t.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

        if (isActive)
        {
            _cache.Set(key, true, PositiveCacheDuration);
            await TouchLastActiveAsync(userId, deviceId, cancellationToken);
        }

        return isActive;
    }

    /// <summary>
    /// 記錄該裝置最後活動時間（線上人數統計依據）。僅於快取未命中時寫入，
    /// 因此每個裝置最多每 <see cref="PositiveCacheDuration"/> 寫入一次；失敗不影響驗證結果。
    /// </summary>
    private async Task TouchLastActiveAsync(string userId, string deviceId, CancellationToken cancellationToken)
    {
        try
        {
            var now = DateTime.UtcNow;
            await _context.UserRefreshTokens
                .Where(t => t.UserId == userId && t.DeviceId == deviceId && !t.IsDead)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.LastActiveAt, now), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 活動時間更新屬輔助資訊，失敗時不得影響請求驗證
        }
    }

    public void InvalidateDevice(string userId, string deviceId)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(deviceId)) return;
        _cache.Remove(DeviceKey(userId, deviceId));
    }

    public void InvalidateUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        _cache.Set(VersionKey(userId), GetVersion(userId) + 1, VersionCacheDuration);
    }

    private string DeviceKey(string userId, string deviceId) =>
        $"sgs:session:{userId}:{GetVersion(userId)}:{deviceId.ToUpperInvariant()}";

    private long GetVersion(string userId) =>
        _cache.TryGetValue(VersionKey(userId), out long version) ? version : 0;

    private static string VersionKey(string userId) => $"sgs:session-ver:{userId}";
}
