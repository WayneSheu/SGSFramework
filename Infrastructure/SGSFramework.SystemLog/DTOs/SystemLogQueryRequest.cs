using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.DTOs
{
    /// <summary>
    /// 系統日誌查詢條件
    /// </summary>
    public record SystemLogQueryRequest : PagedQueryRequest
    {
        public string? TenantId { get; init; }
        public string? UserId { get; init; }
        public string? ModuleName { get; init; }
    }
}
