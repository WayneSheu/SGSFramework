using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using System.ComponentModel;

namespace SGS.Modules.PMS.Infrastructure.Entities;

/// <summary>專案單號流水號：以樂觀並行遞增，防止併發取得重複單號。</summary>
[DisplayName("單號流水號")]
[Comment("專案單號流水號（每專案一列），以 RowVersion 樂觀並行遞增")]
public class TicketSequence : IAuditable
{
    public Guid ProjectId { get; set; }

    [Comment("最後使用的流水號")]
    public int LastNo { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}

/// <summary>客戶問題單（SOP 6）：單號 <c>專案代號-流水號</c>，有單號才動工。</summary>
[DisplayName("問題單")]
[Comment("客戶問題單：單號、分流、SLA、修復與驗證（修復人≠驗證人）")]
public class Ticket : IAuditable
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }

    [Comment("單號（專案代號-流水號，例 PRJ-001）")]
    public string TicketNo { get; set; } = string.Empty;

    [Comment("類型：諮詢／CR／異常／資料撈取")]
    public TicketType Type { get; set; } = TicketType.Incident;

    [Comment("優先級（僅系統異常）")]
    public TicketPriority? Priority { get; set; }

    [Comment("受理時專案所處期別（UAT／SLA）")]
    public ProjectStage Stage { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.Received;

    public string Title { get; set; } = string.Empty;

    [Comment("問題描述（敏感，列表不輸出）")]
    public string Description { get; set; } = string.Empty;

    [Comment("發生環境（正式／UAT）")]
    public string? Environment { get; set; }

    [Comment("影響程度（完全無法作業／有替代方式）")]
    public string? Impact { get; set; }

    public TicketSource Source { get; set; } = TicketSource.Manual;

    [Comment("客戶可見：Stakeholder 僅得見自己提出或標示可見者")]
    public bool CustomerVisible { get; set; } = true;

    [Comment("提出人（建單者）使用者識別碼")]
    public Guid ReporterUserId { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    [Comment("首次回應時間（分流時寫入）")]
    public DateTimeOffset? FirstResponseAt { get; set; }

    [Comment("應解決期限（分流時依 SLA 計算；CR 為空）")]
    public DateTimeOffset? DueAt { get; set; }

    [Comment("實際解決時間（PM 結案時寫入）")]
    public DateTimeOffset? ResolvedAt { get; set; }

    public Guid? FixerUserId { get; set; }
    public Guid? VerifierUserId { get; set; }

    [Comment("關聯 CR 識別碼（CR 模組完成後使用）")]
    public Guid? ChangeRequestId { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Project? Project { get; set; }
    public List<TicketEvent> Events { get; set; } = [];
    public List<TicketAttachment> Attachments { get; set; } = [];
}

/// <summary>問題單事件時間軸（僅附加）。</summary>
[DisplayName("問題單事件")]
[Comment("問題單事件時間軸：建立、分流、指派、修復、驗證、結案")]
public class TicketEvent
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public TicketEventType EventType { get; set; }
    public string? Detail { get; set; }
    public Guid ActorUserId { get; set; }

    [Comment("操作人顯示名稱（帳號）")]
    public string ActorName { get; set; } = string.Empty;

    public DateTimeOffset At { get; set; }

    public Ticket? Ticket { get; set; }
}

/// <summary>問題單附件：實體檔案在 FMS（分類 pms-issue），僅存索引。</summary>
[DisplayName("問題單附件")]
[Comment("問題單附件索引：實體檔案儲存於 FMS，僅記錄 FileId")]
public class TicketAttachment : IAuditable
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }

    [Comment("FMS 檔案識別碼")]
    public Guid FileId { get; set; }

    [Comment("檔名（系統依 SOP 5.5：YYYYMMDD_[單號]_檔案說明）")]
    public string FileName { get; set; } = string.Empty;

    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public string? Sha256 { get; set; }

    [Comment("已確認遮罩個資／密碼／金鑰（SOP 8-4）")]
    public bool MaskConfirmed { get; set; }

    public bool IsDeleted { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Ticket? Ticket { get; set; }
}

/// <summary>國定假日／補班日：SLA 工作天計算（補 SOP 公式未涵蓋假日的缺口）。</summary>
[DisplayName("假日")]
[Comment("國定假日與補班日：SLA 工作天計算使用")]
public class Holiday : IAuditable
{
    [Comment("日期（主鍵）")]
    public DateOnly HolidayDate { get; set; }

    public string Name { get; set; } = string.Empty;

    [Comment("是否為工作日：false=放假（平日國定假日）；true=補班（週末上班）")]
    public bool IsWorkday { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
