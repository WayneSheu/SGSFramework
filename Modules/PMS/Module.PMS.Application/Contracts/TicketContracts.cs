using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Application.Contracts;

// ===== 查詢 =====

public sealed record TicketSearchFilter
{
    public Guid? ProjectId { get; init; }
    public IReadOnlyList<TicketStatus>? Status { get; init; }
    public IReadOnlyList<TicketPriority>? Priority { get; init; }
    public IReadOnlyList<TicketType>? Type { get; init; }
    public IReadOnlyList<SlaStatus>? SlaStatus { get; init; }
    public IReadOnlyList<TicketSource>? Source { get; init; }

    /// <summary>修復人或驗證人。</summary>
    public Guid? AssigneeUserId { get; init; }

    /// <summary>僅列「我提出」或「指派給我」的單。</summary>
    public bool MineOnly { get; init; }

    public DateTimeOffset? ReceivedFrom { get; init; }
    public DateTimeOffset? ReceivedTo { get; init; }
}

public sealed record TicketSearchRequest : PagedQueryParameters<TicketSearchFilter>;

public sealed record TicketListDto(
    Guid Id, string TicketNo, Guid ProjectId, string ProjectCode, string ProjectName, TicketType Type, TicketPriority? Priority,
    TicketStatus Status, string Title, TicketSource Source, DateTimeOffset ReceivedAt, DateTimeOffset? DueAt,
    SlaStatus SlaStatus, Guid ReporterUserId, Guid? FixerUserId, Guid? VerifierUserId);

// ===== 詳情 =====

public sealed record TicketAttachmentDto(
    Guid Id, string FileName, string? OriginalFileName, string? ContentType, long FileSize,
    bool MaskConfirmed, bool CanPreview, string CreatedBy, DateTimeOffset CreatedAtUtc);

public sealed record TicketEventDto(Guid Id, TicketEventType EventType, string? Detail, string ActorName, DateTimeOffset At);

/// <summary>目前使用者對此單可執行的動作（供 UI 顯示按鈕；伺服器端仍會重新驗證）。</summary>
public sealed record TicketActionsDto(
    bool CanTriage, bool CanAssign, bool CanResolve, bool CanVerify, bool CanClose, bool CanCancel, bool CanReopen, bool CanUpload);

public sealed record TicketDetailDto(
    Guid Id, string TicketNo, Guid ProjectId, string ProjectCode, TicketType Type, TicketPriority? Priority,
    ProjectStage Stage, TicketStatus Status, string Title, string Description, string? Environment, string? Impact,
    TicketSource Source, bool CustomerVisible, Guid ReporterUserId,
    DateTimeOffset ReceivedAt, DateTimeOffset? FirstResponseAt, DateTimeOffset? FirstResponseDueAt,
    DateTimeOffset? DueAt, DateTimeOffset? ResolvedAt, SlaStatus SlaStatus,
    Guid? FixerUserId, Guid? VerifierUserId, bool RcaRequired, Guid? ChangeRequestId, string? ChangeRequestNo,
    IReadOnlyList<TicketAttachmentDto> Attachments, IReadOnlyList<TicketEventDto> Events,
    TicketActionsDto Actions, string RowVersion);

// ===== 動作請求 =====

/// <param name="Type">提出人選的問題類型（省略時為系統異常）；最終類型與優先級仍由 PM 於分流時判定。</param>
public sealed record CreateTicketRequest(
    Guid ProjectId, string Title, string Description, string? Environment, string? Impact, bool? CustomerVisible,
    TicketType? Type = null);

/// <param name="CrKind">分流為「需求變更」時必填：客戶 CR（報價、需客戶簽核）或內部 CR。</param>
public sealed record TriageTicketRequest(
    TicketType Type, TicketPriority? Priority, string? Note, string RowVersion, CrKind? CrKind = null);

public sealed record AssignTicketRequest(Guid FixerUserId, Guid VerifierUserId, string RowVersion);

public sealed record ResolveTicketRequest(
    string Summary, bool ReproTestAdded, string? PullRequestUrl, bool? SpecUpdated, string RowVersion);

public sealed record VerifyTicketRequest(bool Passed, string? Note, bool RegressionPassed, string RowVersion);

public sealed record CloseTicketRequest(string? ReleaseNotesRef, string? CloseNote, string RowVersion);

public sealed record ReopenTicketRequest(string Reason, string RowVersion);

public sealed record CancelTicketRequest(string Reason, string RowVersion);

// ===== 假日 =====

public sealed record HolidaySearchRequest(int? Year);

public sealed record HolidayDto(DateOnly Date, string Name, bool IsWorkday);

public sealed record UpsertHolidaysRequest(IReadOnlyList<HolidayDto> Items);

public sealed record UpsertHolidaysResultDto(int Upserted);
