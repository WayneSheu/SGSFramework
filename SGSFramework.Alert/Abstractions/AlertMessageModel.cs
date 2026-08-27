using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Alert.Abstractions
{
    /// <summary>
    /// 跨模組標準告警訊息模型
    /// </summary>
    public sealed record AlertMessageModel
    {
        public required string AlertId { get; init; }
        public required string SourceModule { get; init; }
        public required string AlertCode { get; init; }
        public required string Message { get; init; }
        public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
        
        public string? ExceptionDetails { get; init; }

        /// <summary>
        /// 計算告警唯一特徵鍵 (用於防洪冷卻機制)
        /// </summary>
        public string GetFingerprintKey()
        {
            return $"{SourceModule}:{AlertCode}:{Message.GetHashCode(StringComparison.Ordinal)}";
        }
    }
}
