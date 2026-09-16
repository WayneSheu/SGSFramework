using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.DTOs
{
    /// <summary>
    /// 通用分頁查詢基礎請求
    /// </summary>
    public record PagedQueryRequest
    {
        public int PageIndex { get; init; } = 1;
        public int PageSize { get; init; } = 20;
        public DateTimeOffset? StartTime { get; init; }
        public DateTimeOffset? EndTime { get; init; }
        public string? Keyword { get; init; }
        public string? Level { get; init; }
        public string? CorrelationId { get; init; }
    }
}
