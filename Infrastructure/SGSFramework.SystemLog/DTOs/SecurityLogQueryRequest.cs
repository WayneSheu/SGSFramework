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
        // 額外需要的專屬參數可直接寫在此處（若全數歸納於 Filter 則可省略）
    }
}
