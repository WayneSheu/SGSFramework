using SGSFramework.Core.Paginations;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.DTOs
{
    /// <summary>
    /// 系統日誌查詢條件請求
    /// </summary>
    public record SystemLogQueryRequest : PagedQueryParameters<SystemLogFilter>
    {
        /// <summary>
        /// 日誌來源（可多選）：熱庫（hot）與年度換庫後的冷庫（cold-xxx）；空值 = 熱庫。
        /// 多來源時依時間合併，排序僅支援 Timestamp。可用來源由 GET log-manager/sources 取得。
        /// </summary>
        public IReadOnlyList<string>? Sources { get; init; }

        // 專屬屬性已交由 SystemLogFilter 統一管理，此處直接繼承即可享有 PageIndex, PageSize, Sorts, Type 與 Keyset 游標
    }
}
