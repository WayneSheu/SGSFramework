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
        // 專屬屬性已交由 SystemLogFilter 統一管理，此處直接繼承即可享有 PageIndex, PageSize, Sorts, Type 與 Keyset 游標
    }
}
