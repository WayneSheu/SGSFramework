using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SGSFramework.Core.Reports
{ 
    /// <summary>
    /// 支援 IReportData 介面的泛型快取服務
    /// </summary>
    public interface IReportCacheService
    {
        Task SetCacheAsync<T>(string cacheKey, T data, TimeSpan? expiration = null) where T : IReportData;
        Task<T?> GetCacheAsync<T>(string cacheKey) where T : class, IReportData;
        Task RemoveCacheAsync(string cacheKey);
    }
}
