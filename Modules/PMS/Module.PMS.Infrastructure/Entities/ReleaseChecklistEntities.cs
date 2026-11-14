using System.ComponentModel;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;

namespace SGS.Modules.PMS.Infrastructure.Entities;

/// <summary>檢核項等級（SOP 附錄 A）：阻斷項須全數通過方可上線；警告項未完成須附例外放行備註。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReleaseSeverity
{
    Blocking = 1,
    Warning = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReleaseItemResult
{
    Pending = 0,
    Pass = 1,
    Fail = 2,
    NotApplicable = 3
}

/// <summary>放行簽核角色：交叉驗證工程師（非主要作者）→ Tech Lead 放行 → PM 確認。</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReleaseSignRole
{
    CrossVerifier = 1,
    TechLead = 2,
    Pm = 3
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReleaseChecklistStatus
{
    /// <summary>檢核中（可更新檢核項）。</summary>
    Draft = 0,

    /// <summary>Tech Lead 已放行（Release Gate 已通過），內容鎖定，待 PM 確認。</summary>
    Released = 1,

    /// <summary>PM 已確認，全部簽核完成。</summary>
    Confirmed = 2
}

/// <summary>上線前資安與發布檢核表（FORM-SEC-01，SOP 附錄 A）：由範本展開 SEC-01~11、REL-01~06。</summary>
[DisplayName("上線檢核表")]
[Comment("上線前資安與發布檢核表：版本、預計上線日、主要作者與三方放行簽核")]
public class ReleaseChecklist : IAuditable
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }

    [Comment("版更版本號（vX.Y.Z）")]
    public string Version { get; set; } = string.Empty;

    public DateOnly PlannedDate { get; set; }

    [Comment("本次變更主要作者（交叉驗證者不得為此人）")]
    public Guid MainAuthorUserId { get; set; }

    public ReleaseChecklistStatus Status { get; set; } = ReleaseChecklistStatus.Draft;

    [Comment("例外放行備註（有警告項未完成時 Tech Lead 放行須填原因與預計補齊日）")]
    public string? ExceptionNote { get; set; }

    public Guid CreatedByUserId { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Project? Project { get; set; }
    public List<ReleaseChecklistItem> Items { get; set; } = [];
    public List<ReleaseSignoff> Signoffs { get; set; } = [];
}

[DisplayName("上線檢核項")]
[Comment("上線檢核項：編號、等級（建立時自範本快照）、結果與備註")]
public class ReleaseChecklistItem
{
    public Guid Id { get; set; }
    public Guid ChecklistId { get; set; }

    [Comment("檢核編號（SEC-01…SEC-11、REL-01…REL-06）")]
    public string Code { get; set; } = string.Empty;

    public ReleaseSeverity Severity { get; set; }
    public ReleaseItemResult Result { get; set; } = ReleaseItemResult.Pending;

    [Comment("備註／證據位置（報告歸檔路徑等）")]
    public string? Note { get; set; }

    public Guid? CheckedBy { get; set; }
    public string? CheckedByName { get; set; }
    public DateTimeOffset? CheckedAt { get; set; }

    public ReleaseChecklist? Checklist { get; set; }
}

[DisplayName("上線放行簽核")]
[Comment("上線放行簽核：交叉驗證、Tech Lead 放行、PM 確認")]
public class ReleaseSignoff
{
    public Guid Id { get; set; }
    public Guid ChecklistId { get; set; }
    public ReleaseSignRole Role { get; set; }
    public Guid SignerUserId { get; set; }
    public string SignerName { get; set; } = string.Empty;
    public DateTimeOffset SignedAt { get; set; }

    [Comment("Tech Lead 放行時的例外備註")]
    public string? ExceptionNote { get; set; }

    public ReleaseChecklist? Checklist { get; set; }
}
