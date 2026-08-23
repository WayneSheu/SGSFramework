using Microsoft.Extensions.Caching.Memory;

namespace SGSFramework.Core.Abstractions.Alerts
{
    public sealed class InMemoryAlertFloodSuppressor : IAlertFloodSuppressor
    {
        private readonly IMemoryCache _memoryCache;
        private static readonly object LockObject = new();

        public InMemoryAlertFloodSuppressor(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        public Task<bool> ShouldSuppressAsync(string alertKey, TimeSpan cooldown, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(alertKey);

            var cacheKey = $"AlertFlood:{alertKey}";

            lock (LockObject)
            {
                if (_memoryCache.TryGetValue(cacheKey, out _))
                {
                    // 存在記錄 -> 觸發防洪抑制
                    return Task.FromResult(true);
                }

                // 無記錄 -> 設定 Cache 並且設定絕對到期時間為冷卻期
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(cooldown);

                _memoryCache.Set(cacheKey, true, cacheOptions);

                return Task.FromResult(false);
            }
        }
    }
}
