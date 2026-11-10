using SGSFramework.Core.Paginations;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.DTOs
{
    /// <summary>
    /// 安全性日誌查詢條件請求
    /// </summary>
    public record SecurityLogQueryRequest : PagedQueryParameters<SecurityLogFilter>
    {
        /// <summary>
        /// 日誌來源（可多選）：熱庫（hot）與年度換庫後的冷庫（cold-xxx）；空值 = 熱庫。
        /// 多來源時依時間合併，排序僅支援 Timestamp。可用來源由 GET log-manager/sources 取得。
        /// </summary>
        public IReadOnlyList<string>? Sources { get; init; }

        // 額外需要的專屬參數可直接寫在此處（若全數歸納於 Filter 則可省略）
    }
}
