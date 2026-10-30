// Path: Infrastructure/SGSFramework.SystemLog/DTOs/SystemLogDto.cs
namespace SGSFramework.SystemLog.DTOs;

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
    string? TenantName,     // 💡 新增：實驗室名稱
    string? UserId,
    string? UserName,       // 💡 新增：使用者真實姓名
    string? ModuleName,
    string? Operation,
    string? OperationDesc,  // 💡 新增：組合操作說明
    string? ControllerName, // 💡 新增：控制器名稱
    string? CorrelationId,
    string? IP,
    string? Url,
    string? Payload,
    string? AlertId,
    DateTimeOffset CreatedAt
);