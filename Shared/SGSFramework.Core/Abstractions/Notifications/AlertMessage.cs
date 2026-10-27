using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Notifications
{
    /// <summary>
    /// 統一告警訊息領域實體
    /// </summary>
    public sealed class AlertMessage
    {
        public string AlertId { get; init; } = Guid.NewGuid().ToString("N");
        public string CorrelationId { get; set;  } = Guid.NewGuid().ToString("N");
        public string LogType { get; init; } = "System";
        public string LogLevel { get; init; } = "Error";
        public string MessageTemplate { get; init; } = string.Empty;
        public string RenderedMessage { get; init; } = string.Empty;
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
        public string? ExceptionDetails { get; init; }
        public string Fingerprint { get; init; } = string.Empty; // 用於錯誤去重防洪的特徵碼

        /// <summary>觸發此告警的規則名稱（動態告警規則）</summary>
        public string? RuleName { get; init; }

        /// <summary>此告警的專屬收件人（逗號分隔 Email）；空值沿用全域收件人</summary>
        public string? Recipients { get; init; }

    }
}
