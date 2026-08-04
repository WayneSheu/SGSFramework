using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGSFramework.Core.Reports
{
    /// <summary>
    /// 報表快取服務
    /// </summary>
    public class ReportCacheService : IReportCacheService
    {
        private readonly IMemoryCache _cache;
        private const string CachePrefix = "RPT_";

        public ReportCacheService(IMemoryCache cache) => _cache = cache;

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cacheKey"></param>
        /// <param name="data"></param>
        /// <param name="expiration">絕對到期時間:預設改為15分鐘</param>
        /// <returns></returns>
        public async Task SetCacheAsync<T>(string cacheKey, T data, TimeSpan? expiration = null) where T : IReportData
        {
            // 強制轉換與凍結資料的邏輯
            object dataToCache = data;

            // 如果傳入的是尚未執行的查詢 (IEnumerable 但不是 List/Array)
            if (data is IEnumerable<object> enumerable && data is not IList<object> && data is not Array)
            {
                // 強制執行 ToList() 以凍結資料庫連線
                dataToCache = enumerable.ToList();
            }

            // 如果是單一物件 (如 CTPL_CLM_MedicalInfo)，通常其內部的 Details 已在 DTO 組裝時轉為 List
            // 這部分建議在 DTO 定義時就強制使用 List<T> 而非 IEnumerable<T>
            var options = new MemoryCacheEntryOptions
            {
                //絕對到期時間，外部動態設定的彈性，預設改為 15 或 20 分鐘
                AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(15),
                //滑動過期，避免閒置資料佔用記憶體
                SlidingExpiration = TimeSpan.FromMinutes(5),
                //High 優先權，因為報表資料查庫與組裝 DTO 較耗資源
                Priority = CacheItemPriority.High
            };

            _cache.Set($"{CachePrefix}{cacheKey}", dataToCache, options);
            await Task.CompletedTask;

        }



        /// <summary>
        /// 使用泛型 T，並約束 T 必須實作 IReportData
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cacheKey"></param>
        /// <returns></returns>
        public async Task<T?> GetCacheAsync<T>(string cacheKey) where T : class, IReportData
        {
            _cache.TryGetValue($"{CachePrefix}{cacheKey}", out T? data);
            return await Task.FromResult(data);
        }

        public async Task RemoveCacheAsync(string cacheKey)
        {
            _cache.Remove($"{CachePrefix}{cacheKey}");
            await Task.CompletedTask;
        }
    }
}
