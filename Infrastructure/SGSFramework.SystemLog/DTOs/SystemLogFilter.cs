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

        /// <summary>關聯識別碼（前綴比對，可貼上完整或部分值）</summary>
        public string? CorrelationId { get; init; }

        /// <summary>操作名稱（包含比對）</summary>
        public string? Operation { get; init; }
        public DateTimeOffset? StartTimestamp { get; init; }
        public DateTimeOffset? EndTimestamp { get; init; }
    }
}
