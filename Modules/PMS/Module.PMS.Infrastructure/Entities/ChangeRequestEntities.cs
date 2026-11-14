using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using System.ComponentModel;

namespace SGS.Modules.PMS.Infrastructure.Entities;

/// <summary>CR 單號流水號（專案代號-CR-流水號），以樂觀並行遞增。</summary>
[DisplayName("CR 單號流水號")]
[Comment("CR 單號流水號（每專案一列），以 RowVersion 樂觀並行遞增")]
public class ChangeRequestSequence : IAuditable
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

/// <summary>需求變更單（FORM-CR-01，SOP 第 4 章）：受理 → 影響評估 → 決策 → 簽核。</summary>
[DisplayName("需求變更單")]
[Comment("需求變更單 CR：客戶 CR（報價）與內部 CR；評估、決策與簽核")]
public class ChangeRequest : IAuditable
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }

    [Comment("CR 單號（專案代號-CR-流水號，例 PRJ-CR-001）")]
    public string CrNo { get; set; } = string.Empty;

    public CrKind Kind { get; set; }
    public CrStatus Status { get; set; } = CrStatus.Received;

    public string Title { get; set; } = string.Empty;

    [Comment("需求描述")]
    public string Description { get; set; } = string.Empty;

    [Comment("來源問題單（由問題單分流為 CR 而建立時）")]
    public Guid? SourceTicketId { get; set; }

    public Guid RequesterUserId { get; set; }

    public DateTimeOffset ReceivedAt { get; set; }

    [Comment("影響評估期限：受理後 2 個工作天（SOP 4.4）")]
    public DateTimeOffset EvalDueAt { get; set; }

    [Comment("最近一次評估逾期提醒時間（同一張 CR 每個台灣日最多提醒一次；重啟時清除）")]
    public DateTimeOffset? LastEvalReminderAt { get; set; }

    [Comment("決策方案")]
    public CrDecision? Decision { get; set; }

    [Comment("決策說明")]
    public string? DecisionRationale { get; set; }

    [Comment("方案 A：新的上線日")]
    public DateOnly? NewReleaseDate { get; set; }

    [Comment("方案 B：移出本次迭代的原定任務")]
    public string? SwappedOutItems { get; set; }

    [Comment("客戶 CR 報價金額")]
    public decimal? QuoteAmount { get; set; }

    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }

    [Comment("決策時內容的 SHA-256（簽核者看到的內容必須與之相符，簽核後內容鎖定）")]
    public string? ContentHash { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public Project? Project { get; set; }
    public CrImpactAssessment? Assessment { get; set; }
    public List<CrSignoff> Signoffs { get; set; } = [];
    public List<CrEvent> Events { get; set; } = [];
}

/// <summary>Tech Lead 影響評估（SOP 4.3）：層級、API 契約、模組耦合、資安與工時。</summary>
[DisplayName("CR 影響評估")]
[Comment("CR 影響評估：受影響層級、API 契約、模組耦合、資安影響與 BE／FE／驗證工時")]
public class CrImpactAssessment : IAuditable
{
    public Guid Id { get; set; }
    public Guid ChangeRequestId { get; set; }

    [Comment("受影響層級（逗號分隔：Presentation、Api、Application、Domain、Infrastructure）")]
    public string AffectedLayers { get; set; } = string.Empty;

    public CrApiImpact ApiImpact { get; set; }
    public CrCouplingImpact CouplingImpact { get; set; }

    [Comment("資安影響：是則須 Design Gate 重審")]
    public bool SecurityImpact { get; set; }

    public decimal HoursBe { get; set; }
    public decimal HoursFe { get; set; }
    public decimal HoursVerify { get; set; }

    public string? Note { get; set; }

    public Guid EvaluatedBy { get; set; }
    public DateTimeOffset EvaluatedAt { get; set; }

    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public ChangeRequest? ChangeRequest { get; set; }
}

/// <summary>CR 簽核紀錄：內容雜湊與時間；客戶簽核目前由 PM 代錄（IsProxy）。</summary>
[DisplayName("CR 簽核")]
[Comment("CR 簽核紀錄：PM、Tech Lead 與客戶；含簽核當下內容雜湊，客戶簽核可由 PM 代錄")]
public class CrSignoff
{
    public Guid Id { get; set; }
    public Guid ChangeRequestId { get; set; }
    public CrSignRole Role { get; set; }

    [Comment("簽核人使用者識別碼（客戶代錄時為代錄的 PM）")]
    public Guid SignerUserId { get; set; }

    public string SignerName { get; set; } = string.Empty;

    [Comment("簽核當下的內容 SHA-256")]
    public string ContentHash { get; set; } = string.Empty;

    public DateTimeOffset SignedAt { get; set; }

    [Comment("是否由 PM 代錄客戶簽核（例外路徑，須附原因）")]
    public bool IsProxy { get; set; }

    public string? Comment { get; set; }

    public ChangeRequest? ChangeRequest { get; set; }
}

/// <summary>CR 事件時間軸（僅附加）。</summary>
[DisplayName("CR 事件")]
[Comment("CR 事件時間軸：建立、評估、決策、簽核、核准、取消")]
public class CrEvent
{
    public Guid Id { get; set; }
    public Guid ChangeRequestId { get; set; }
    public CrEventType EventType { get; set; }
    public string? Detail { get; set; }
    public Guid ActorUserId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public DateTimeOffset At { get; set; }

    public ChangeRequest? ChangeRequest { get; set; }
}
