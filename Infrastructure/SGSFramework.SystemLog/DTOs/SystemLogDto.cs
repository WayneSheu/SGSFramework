using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.DTOs
{
    /// <summary>
    /// 系統日誌 DTO
    /// </summary>
    public record SystemLogDto(
        long Id,
        DateTimeOffset TimeStamp,
        string? Level,
        string? Message,
        string? Exception,
        string? TenantId,
        string? UserId,
        string? ModuleName,
        string? Operation,
        string? CorrelationId,
        string? IP,
        string? Url,
        string? Payload,
        string? AlertId,
        DateTimeOffset CreatedAt
    );
}
