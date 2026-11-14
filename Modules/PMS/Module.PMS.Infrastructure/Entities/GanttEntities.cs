using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;

namespace SGS.Modules.PMS.Infrastructure.Entities;

/// <summary>
/// 專案 WBS 任務（SOP 啟動規劃：WBS、甘特圖）。有子任務者為「摘要任務」，其起迄與進度由子任務彙總（讀取時計算，不以自身欄位為準）。
/// 日期為日曆日（含起訖日）；里程碑的起迄同一天。刪除為軟刪除（連同子任務）。
/// </summary>
[DisplayName("專案任務")]
[Comment("專案 WBS 任務：階層、起迄日、負責人、進度；軟刪除")]
public class ProjectTask : IAuditable
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }

    [Comment("上層任務（空為頂層）")]
    public Guid? ParentId { get; set; }

    [Comment("同一上層下的排序（由小到大）")]
    public int SortOrder { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Comment("開始日（含）")]
    public DateOnly StartDate { get; set; }

    [Comment("結束日（含）")]
    public DateOnly EndDate { get; set; }

    [Comment("是否為里程碑（起迄同一天）")]
    public bool IsMilestone { get; set; }

    [Comment("負責人（須為專案內部成員）；空為未指派")]
    public Guid? AssigneeUserId { get; set; }

    [Comment("完成百分比 0~100")]
    public byte PercentComplete { get; set; }

    [Comment("對應的 SSDLC 階段（選填，供階段分組）")]
    public ProjectPhase? Phase { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Project? Project { get; set; }
}

/// <summary>任務前置相依（完成後才開始，Finish-to-Start）：後繼任務的開始日須晚於前置任務結束日加落後天數。僅限無子任務的任務之間。</summary>
[DisplayName("任務相依")]
[Comment("任務前置相依（FS）：後繼任務須在前置任務結束後（加落後天數）才開始")]
public class ProjectTaskDependency
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid PredecessorId { get; set; }
    public Guid SuccessorId { get; set; }

    [Comment("落後天數（前置結束日後再等幾天；0＝次日即可開始）")]
    public int LagDays { get; set; }

    public ProjectTask? Predecessor { get; set; }
    public ProjectTask? Successor { get; set; }
}

/// <summary>排程基準線：計畫確定時對所有任務（含摘要彙總後的起迄）拍下的快照，供之後對照偏差。</summary>
[DisplayName("專案基準線")]
[Comment("專案排程基準線：任務起迄快照，供對照延後或提前")]
public class ProjectBaseline : IAuditable
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }

    [Comment("基準線序號（專案內遞增，B1、B2…）")]
    public int Number { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Note { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Project? Project { get; set; }
    public List<ProjectBaselineTask> Tasks { get; set; } = [];
}

[DisplayName("基準線任務")]
[Comment("基準線的任務快照（任務識別碼、名稱與起迄日）")]
public class ProjectBaselineTask
{
    public Guid Id { get; set; }
    public Guid BaselineId { get; set; }

    [Comment("拍照當時的任務識別碼（任務之後被刪除時仍可保留對照）")]
    public Guid TaskId { get; set; }

    public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsMilestone { get; set; }

    public ProjectBaseline? Baseline { get; set; }
}
