// Path: Infrastructure/SGSFramework.SystemLog/DTOs/SystemLogFilter.cs
namespace SGSFramework.SystemLog.DTOs;

/// <summary>
/// 系統日誌篩選條件
/// </summary>
public class SystemLogFilter
{
    public string? Level { get; set; }
    public string? TenantId { get; set; }
    public string? TenantName { get; set; } // 💡 新增：實驗室名稱篩選
    public string? UserId { get; set; }
    public string? UserName { get; set; } // 💡 新增：使用者真實姓名篩選
    public string? ModuleName { get; set; }
    public string? Operation { get; set; }
    public string? OperationDesc { get; set; } // 💡 新增：操作說明篩選
    public string? ControllerName { get; set; } // 💡 新增：Controller 名稱篩選
    public string? CorrelationId { get; set; }
    public string? AlertId { get; set; }
    public DateTimeOffset? StartTimestamp { get; set; }
    public DateTimeOffset? EndTimestamp { get; set; }
}