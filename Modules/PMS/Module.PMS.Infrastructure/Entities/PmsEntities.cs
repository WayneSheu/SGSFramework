using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using System.ComponentModel;

namespace SGS.Modules.PMS.Infrastructure.Entities;

/// <summary>專案主檔：專案代號由 PMS 核發且不可變。</summary>
[DisplayName("專案")]
[Comment("專案主檔：專案代號由 PMS 核發，建立後不可修改")]
public class Project : IAuditable
{
    [Comment("識別主鍵")]
    public Guid Id { get; set; }

    [Comment("專案代號（2~10 碼大寫英數、首碼為字母），核發後不可變")]
    public string Code { get; set; } = string.Empty;

    [Comment("專案名稱")]
    public string Name { get; set; } = string.Empty;

    [Comment("目前階段（六階段）")]
    public ProjectPhase Phase { get; set; } = ProjectPhase.Initiation;

    [Comment("期別：UAT 驗收期／SLA 維運期")]
    public ProjectStage Stage { get; set; } = ProjectStage.Uat;

    [Comment("專案狀態")]
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;

    [Comment("PM 使用者識別碼")]
    public Guid PmUserId { get; set; }

    [Comment("Tech Lead 使用者識別碼")]
    public Guid TechLeadUserId { get; set; }

    [Comment("需求凍結線：此日期後任何規格變更須走 CR")]
    public DateOnly? FreezeLineDate { get; set; }

    [Comment("合約 SLA 覆寫備註（以合約為準時註記）")]
    public string? ContractSlaNote { get; set; }

    [Comment("並行控制")]
    public byte[] RowVersion { get; set; } = [];

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public List<ProjectMember> Members { get; set; } = [];
    public List<PmsModule> Modules { get; set; } = [];
    public List<ProjectGate> Gates { get; set; } = [];
    public List<Deliverable> Deliverables { get; set; } = [];
}

/// <summary>專案成員與 RACI 角色（僅存 UserId，名稱經 ORG 契約解析，ARC-01）。</summary>
[DisplayName("專案成員")]
[Comment("專案成員與 RACI 角色；亦為資料列授權依據")]
public class ProjectMember : IAuditable
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public RaciRole RaciRole { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Project? Project { get; set; }
}

/// <summary>專案模組目錄（Mxx_模組名稱，SOP 5.3）。</summary>
[DisplayName("專案模組")]
[Comment("專案模組目錄：Mxx 代號於文件資料夾與命名共用")]
public class PmsModule : IAuditable
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }

    [Comment("模組代號 Mxx")]
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Project? Project { get; set; }
}

/// <summary>SSDLC 卡控節點放行紀錄（A=Tech Lead）。</summary>
[DisplayName("專案卡控節點")]
[Comment("SSDLC 四道卡控節點放行紀錄")]
public class ProjectGate : IAuditable
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public GateKind Gate { get; set; }
    public GateStatus Status { get; set; } = GateStatus.Pending;
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? EvidenceNote { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Project? Project { get; set; }
}

/// <summary>交付物索引：檔案本體在 FMS（僅存 FileId），不跨 schema FK（ARC-01）。</summary>
[DisplayName("交付物")]
[Comment("交付物索引：附件實體儲存於 FMS，僅記錄 FileId")]
public class Deliverable : IAuditable
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ModuleId { get; set; }
    public ProjectPhase Stage { get; set; }

    [Comment("文件類型（SRS、OpenAPI、ERD、SAST…）")]
    public string DocType { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    [Comment("FMS 檔案識別碼")]
    public Guid FileId { get; set; }

    [Comment("檔名（系統依命名鐵律產生）")]
    public string FileName { get; set; } = string.Empty;

    [Comment("文件名稱（命名用，如：系統需求規格書）")]
    public string? DocName { get; set; }

    [Comment("使用者上傳時的原始檔名")]
    public string? OriginalFileName { get; set; }

    [Comment("內容類型")]
    public string? ContentType { get; set; }

    [Comment("檔案大小（位元組）；NULL 表示 FMS 無實檔（舊版僅登記索引）")]
    public long? FileSize { get; set; }

    [Comment("內容 SHA-256（十六進位）")]
    public string? Sha256 { get; set; }

    [Comment("是否已刪除（虛擬刪除；FMS 實體檔案於保留期滿後由排程清除）")]
    public bool IsDeleted { get; set; }

    public DateTimeOffset? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }

    /// <summary>是否有可檢視／下載的實體檔案（舊版僅登記索引者為 false）。</summary>
    public bool HasFile => FileSize.HasValue;

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Project? Project { get; set; }
    public PmsModule? Module { get; set; }
}

/// <summary>專案代號保留表：已使用或作廢的代號永不重用，避免單號與歷史附件混淆。</summary>
[DisplayName("專案代號保留")]
[Comment("專案代號保留表：核發後永不重用")]
public class ProjectCodeReservation : IAuditable
{
    [Comment("專案代號（主鍵）")]
    public string Code { get; set; } = string.Empty;

    public ProjectCodeStatus Status { get; set; } = ProjectCodeStatus.Reserved;
    public Guid? ProjectId { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
