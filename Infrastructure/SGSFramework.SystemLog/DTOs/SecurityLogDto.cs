using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.DTOs
{
    /// <summary>
    /// 安全性審計日誌 DTO
    /// </summary>
    public record SecurityLogDto(
        int Id,
        DateTimeOffset Timestamp,
        string? Level,
        string? Message,
        string? Exception,
        string? LogType,
        string? EventCategory,
        string? UserId,
        string? ClientIp,
        string? CorrelationId,
        string? Properties,
        string? AlertId
    );
}
