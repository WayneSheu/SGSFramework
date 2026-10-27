using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.Entities.SystemAccounts;
using System;

namespace SGSFramework.Identity.Services
{
    /// <summary>
    /// 系統帳號權杖即時有效性檢查：帳號狀態與撤銷時間點以 IMemoryCache 快取 30 秒，避免每個請求都查詢資料庫。
    /// 本機異動由 SystemAccountService 立即清除快取；多節點（K8s 多 Pod）部署時其他節點最長延遲 30 秒生效。
    /// </summary>
    public sealed class SystemAccountTokenValidator(DbContext dbContext, IMemoryCache cache) : ISystemAccountTokenValidator
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

        private readonly DbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));

        public static string CacheKey(string accountId) => $"system-account:token-state:{accountId}";

        public async Task<string?> ValidateAsync(string accountId, DateTimeOffset issuedAt, CancellationToken cancellationToken = default)
        {
            var state = await _cache.GetOrCreateAsync(CacheKey(accountId), async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheDuration;
                return await _dbContext.Set<SystemAccount>()
                    .AsNoTracking()
                    .Where(a => a.Id == accountId)
                    .Select(a => new TokenState(a.Status, a.TokensNotBefore))
                    .FirstOrDefaultAsync(cancellationToken);
            });

            if (state is null)
            {
                return "系統帳號不存在或已刪除。";
            }

            if (state.Status != SystemAccountStatus.Active)
            {
                return state.Status == SystemAccountStatus.Frozen ? "系統帳號已凍結。" : "系統帳號已停用。";
            }

            return issuedAt < state.TokensNotBefore ? "系統帳號權杖已被撤銷，請重新換發。" : null;
        }

        private sealed record TokenState(SystemAccountStatus Status, DateTimeOffset TokensNotBefore);
    }
}
