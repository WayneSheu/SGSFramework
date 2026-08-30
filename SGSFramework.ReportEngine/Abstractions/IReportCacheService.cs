using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGSFramework.ReportEngine.Abstractions
{
    /// <summary>
    /// 支援 IReportData 介面的泛型快取服務
    /// </summary>
    public interface IReportCacheService
    {
        Task SetCacheAsync<T>(string cacheKey, T data, TimeSpan? expiration = null) where T : IReportData;
        Task<T?> GetCacheAsync<T>(string cacheKey) where T : class, IReportData;
        Task RemoveCacheAsync(string cacheKey);

        /// <summary>
        /// 準備並快取報表資料，自動產生與回傳唯一 cacheKey（支援所有實作 IReportData 的泛型 DTO）
        /// </summary>
        Task<string> PrepareAndCacheReportAsync<T>(T reportData, TimeSpan? expiration = null) where T : class, IReportData;
    }
}
