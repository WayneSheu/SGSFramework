using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;

namespace SGS.Modules.PMS.Infrastructure.Entities;

/// <summary>風險等級（SOP 3.1 風險管理表）。數值愈大愈嚴重，供排序使用。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RiskStatus
{
    /// <summary>已識別，尚未開始因應。</summary>
    Open = 0,

    /// <summary>因應中。</summary>
    Mitigating = 1,

    /// <summary>已解除或已接受。</summary>
    Closed = 2
}

/// <summary>專案風險管理表的一筆風險（SOP 3.1：PM 風險追蹤，歸檔 01_專案管理/04_變更與風險）。僅內部成員可見（客戶 Stakeholder 不可見）。</summary>
[DisplayName("專案風險")]
[Comment("專案風險：標題、等級、因應措施、負責人與狀態；作廢為軟刪除")]
public class RiskItem : IAuditable
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }

    [Comment("風險標題")]
    public string Title { get; set; } = string.Empty;

    public RiskLevel Level { get; set; } = RiskLevel.Medium;
    public RiskStatus Status { get; set; } = RiskStatus.Open;

    [Comment("因應措施（緩解、移轉、接受或迴避的做法）")]
    public string? Mitigation { get; set; }

    [Comment("風險負責人（須為專案內部成員）")]
    public Guid OwnerUserId { get; set; }

    [Comment("狀態改為已結案的時間；重新開啟時清除")]
    public DateTimeOffset? ClosedAt { get; set; }

    [Comment("是否已作廢（軟刪除，保留稽核）")]
    public bool IsVoided { get; set; }

    public DateTimeOffset? VoidedAt { get; set; }
    public Guid? VoidedBy { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Project? Project { get; set; }
}
