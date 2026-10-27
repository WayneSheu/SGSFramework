using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.DTOs
{
    /// <summary>
    /// 安全性日誌篩選條件
    /// </summary>
    public record SecurityLogFilter
    {
        public string? Level { get; init; }
        public string? EventCategory { get; init; }
        public string? AlertId { get; init; }
        /// <summary>追蹤碼（前綴比對，可含或不含連字號）</summary>
        public string? CorrelationId { get; init; }
        public string? UserId { get; init; }
        public DateTimeOffset? StartTimestamp { get; init; }
        public DateTimeOffset? EndTimestamp { get; init; }
    }
}
