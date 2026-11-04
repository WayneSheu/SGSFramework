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
        /// <summary>true = 查詢主庫舊日誌表（封存）；預設 false = 查詢日誌外掛的獨立日誌庫（模組未啟用時一律讀主庫）。</summary>
        public bool Archive { get; init; }

        // 額外需要的專屬參數可直接寫在此處（若全數歸納於 Filter 則可省略）
    }
}
