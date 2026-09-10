using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.DTOs
{
    /// <summary>
    /// 前端遙測與未捕捉例外上報資料傳輸物件
    /// </summary>
    public sealed class ClientLogPayloadDto
    {
        /// <summary>
        /// 例外引發時間 (UTC)
        /// </summary>
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// 元件名稱或上下文描述資訊
        /// </summary>
        public string Context { get; set; } = string.Empty;

        /// <summary>
        /// 重試與降級策略標籤
        /// </summary>
        public string Policy { get; set; } = "Default";

        /// <summary>
        /// 例外型別全名
        /// </summary>
        public string ExceptionType { get; set; } = string.Empty;

        /// <summary>
        /// 例外訊息內容
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 堆疊追蹤資訊
        /// </summary>
        public string? StackTrace { get; set; }

        /// <summary>
        /// 例外來源（如模組或組件名稱）
        /// </summary>
        public string? Source { get; set; }
    }
}
