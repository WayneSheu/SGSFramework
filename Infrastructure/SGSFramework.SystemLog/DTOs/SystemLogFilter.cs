using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.DTOs
{
    /// <summary>
    /// 系統日誌篩選條件
    /// </summary>
    public record SystemLogFilter
    {
        public string? Level { get; init; }
        public string? TenantId { get; init; }
        public string? UserId { get; init; }
        public string? ModuleName { get; init; }
        public string? AlertId { get; init; }
        public DateTimeOffset? StartTimestamp { get; init; }
        public DateTimeOffset? EndTimestamp { get; init; }
    }
}
