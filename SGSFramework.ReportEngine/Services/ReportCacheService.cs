using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.ReportEngine.Abstractions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SGSFramework.ReportEngine.Services
{
    /// <summary>
    /// 企業級報表快取服務實作
    /// </summary>
    public class ReportCacheService : IReportCacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<ReportCacheService> _logger;
        private const string CachePrefix = "RPT_";

        public ReportCacheService(IMemoryCache cache, ILogger<ReportCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// 寫入報表資料至快取，並自動產生與回傳唯一 cacheKey
        /// </summary>
        public async Task<string> PrepareAndCacheReportAsync<T>(T data, TimeSpan? expiration = null) where T : class, IReportData
        {
            string cacheKey = Guid.NewGuid().ToString("N");
            await SetCacheAsync(cacheKey, data, expiration);
            return cacheKey;
        }

        public async Task SetCacheAsync<T>(string cacheKey, T data, TimeSpan? expiration = null) where T : IReportData
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "快取報表資料不得為空。");
            }

            object dataToCache = data;

            // [優化點] 更精準地攔截與凍結尚未執行的 IEnumerable 查詢，避免 EF Core 斷線例外
            // 排除 string 與 byte[] 這種本身也是 IEnumerable 的型別
            if (data is not string && data is not byte[] && data is IEnumerable enumerable)
            {
                // 若為尚未 materialized 的 LINQ 查詢，強制執行 ToList() 凍結資料
                if (data is not IList && data is not ICollection)
                {
                    var listType = typeof(Enumerable).GetMethod("ToList")
                        ?.MakeGenericMethod(enumerable.GetType().GetGenericArguments().FirstOrDefault() ?? typeof(object));

                    if (listType != null)
                    {
                        dataToCache = listType.Invoke(null, new object[] { enumerable }) ?? data;
                    }
                }
            }

            var options = new MemoryCacheEntryOptions
            {
                // 絕對到期時間，預設 15 分鐘
                AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(15),
                // 滑動過期，避免閒置資料佔用記憶體
                SlidingExpiration = TimeSpan.FromMinutes(5),
                // 高優先權，保護昂貴的報表查詢結果不被輕易回收
                Priority = CacheItemPriority.High
            };

            string fullKey = $"{CachePrefix}{cacheKey}";
            _cache.Set(fullKey, dataToCache, options);

            _logger.LogInformation("已成功快取報表資料，CacheKey: {CacheKey}, 絕對過期時間: {Expiration} 分鐘",
                cacheKey, options.AbsoluteExpirationRelativeToNow?.TotalMinutes);

            await Task.CompletedTask;
        }

        public async Task<T?> GetCacheAsync<T>(string cacheKey) where T : class, IReportData
        {
            string fullKey = $"{CachePrefix}{cacheKey}";

            if (_cache.TryGetValue(fullKey, out T? data) && data != null)
            {
                _logger.LogDebug("報表快取命中 (Hit)，CacheKey: {CacheKey}", cacheKey);
                return await Task.FromResult(data);
            }

            _logger.LogWarning("報表快取未命中 (Miss) 或已過期失效，CacheKey: {CacheKey}", cacheKey);
            return await Task.FromResult((T?)null);
        }

        public async Task RemoveCacheAsync(string cacheKey)
        {
            string fullKey = $"{CachePrefix}{cacheKey}";
            _cache.Remove(fullKey);

            _logger.LogInformation("已清除報表快取，CacheKey: {CacheKey}", cacheKey);
            await Task.CompletedTask;
        }
    }
}