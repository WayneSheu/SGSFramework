using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Application.Contracts;

// ===== 查詢 =====

public sealed record CrSearchFilter
{
    public Guid? ProjectId { get; init; }
    public IReadOnlyList<CrStatus>? Status { get; init; }
    public IReadOnlyList<CrKind>? Kind { get; init; }

    /// <summary>僅列「我提出」的 CR。</summary>
    public bool MineOnly { get; init; }

    /// <summary>僅列影響評估已逾期且尚未評估的 CR。</summary>
    public bool EvalOverdue { get; init; }
}

public sealed record CrSearchRequest : PagedQueryParameters<CrSearchFilter>;

public sealed record CrListDto(
    Guid Id, string CrNo, Guid ProjectId, string ProjectCode, string ProjectName, CrKind Kind, CrStatus Status, string Title,
    DateTimeOffset ReceivedAt, DateTimeOffset EvalDueAt, SlaStatus EvalSla, CrDecision? Decision,
    Guid RequesterUserId, string? SourceTicketNo, IReadOnlyList<CrSignRole> PendingSignRoles);

// ===== 詳情 =====

public sealed record CrAssessmentDto(
    IReadOnlyList<string> AffectedLayers, bool HighRisk, CrApiImpact ApiImpact, CrCouplingImpact CouplingImpact, bool SecurityImpact,
    decimal HoursBe, decimal HoursFe, decimal HoursVerify, decimal HoursTotal, string? Note, Guid EvaluatedBy, DateTimeOffset EvaluatedAt);

public sealed record CrSignoffDto(CrSignRole Role, string SignerName, DateTimeOffset SignedAt, bool IsProxy, string? Comment, string ContentHash);

public sealed record CrEventDto(Guid Id, CrEventType EventType, string? Detail, string ActorName, DateTimeOffset At);

/// <summary>目前使用者對此 CR 可執行的動作（供 UI 顯示按鈕；伺服器端仍會重新驗證）。</summary>
public sealed record CrActionsDto(bool CanEvaluate, bool CanDecide, bool CanSign, bool CanProxyCustomerSign, bool CanCancel, bool CanReopen);

public sealed record CrDetailDto(
    Guid Id, string CrNo, Guid ProjectId, string ProjectCode, string ProjectName, CrKind Kind, CrStatus Status,
    string Title, string Description, Guid? SourceTicketId, string? SourceTicketNo, Guid RequesterUserId,
    DateTimeOffset ReceivedAt, DateTimeOffset EvalDueAt, SlaStatus EvalSla, bool AfterFreezeLine,
    CrAssessmentDto? Assessment, bool DesignGateReviewRequired,
    CrDecision? Decision, string? DecisionRationale, DateOnly? NewReleaseDate, string? SwappedOutItems, decimal? QuoteAmount,
    Guid? DecidedBy, DateTimeOffset? DecidedAt, string? ContentHash,
    IReadOnlyList<CrSignRole> RequiredSignRoles, IReadOnlyList<CrSignRole> PendingSignRoles,
    IReadOnlyList<CrSignoffDto> Signoffs, IReadOnlyList<CrEventDto> Events, CrActionsDto Actions, string RowVersion);

// ===== 動作請求 =====

public sealed record CreateCrRequest(Guid ProjectId, CrKind Kind, string Title, string Description, Guid? SourceTicketId = null);

public sealed record EvaluateCrRequest(
    IReadOnlyList<string> AffectedLayers, CrApiImpact ApiImpact, CrCouplingImpact CouplingImpact, bool SecurityImpact,
    decimal HoursBe, decimal HoursFe, decimal HoursVerify, string? Note, string RowVersion);

public sealed record DecideCrRequest(
    CrDecision Decision, bool Defer, decimal? QuoteAmount, DateOnly? NewReleaseDate, string? SwappedOutItems, string? Rationale, string RowVersion);

/// <param name="ContentHash">簽核者看到的內容雜湊，須與目前內容相符（簽核後內容鎖定）。</param>
public sealed record SignCrRequest(string ContentHash, string? Comment, string RowVersion);

public sealed record ProxyCustomerSignRequest(string ContentHash, string Reason, string RowVersion);

public sealed record CancelCrRequest(string Reason, string RowVersion);

/// <summary>重啟暫緩的 CR（下期規劃時）：回到受理並重新起算 2 個工作天評估期限。</summary>
public sealed record ReopenCrRequest(string Reason, string RowVersion);

/// <summary>評估逾期掃描結果（排程以此顯示作業摘要）。</summary>
public sealed record CrEvalWatchResult(int Overdue, int Notified, int ProjectsNotified, int Skipped, string Message);
