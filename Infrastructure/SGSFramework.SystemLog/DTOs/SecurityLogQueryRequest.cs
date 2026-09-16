using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.DTOs
{
    /// <summary>
    /// 安全性日誌查詢條件
    /// </summary>
    public record SecurityLogQueryRequest : PagedQueryRequest
    {
        public string? EventCategory { get; init; }
        public string? UserId { get; init; }
    }
}
