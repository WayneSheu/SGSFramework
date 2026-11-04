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
        /// <summary>true = 查詢主庫舊日誌表（封存）；預設 false = 查詢日誌外掛的獨立日誌庫（模組未啟用時一律讀主庫）。</summary>
        public bool Archive { get; init; }

        // 專屬屬性已交由 SystemLogFilter 統一管理，此處直接繼承即可享有 PageIndex, PageSize, Sorts, Type 與 Keyset 游標
    }
}
