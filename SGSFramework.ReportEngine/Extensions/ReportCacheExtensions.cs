using SGSFramework.ReportEngine.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ReportEngine.Extensions
{
    public static class ReportCacheExtensions
    {
        /// <summary>
        /// 共用的報表資料快取準備方法，供各業務模組呼叫
        /// </summary>
        public static async Task<string> PrepareAndCacheReportAsync<TReport>(
            this IReportCacheService cacheService,
            TReport reportData,
            TimeSpan? expiration = null) where TReport : class, IReportData
        {
            string cacheKey = Guid.NewGuid().ToString("N");
            await cacheService.SetCacheAsync(cacheKey, reportData, expiration);
            return cacheKey;
        }
    }
}
