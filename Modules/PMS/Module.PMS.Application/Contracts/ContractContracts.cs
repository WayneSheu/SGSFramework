using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Application.Contracts;

public sealed record ContractSearchFilter
{
    public IReadOnlyList<ContractStatus>? Status { get; init; }
    public IReadOnlyList<ContractType>? Type { get; init; }
    public IReadOnlyList<ContractIndustry>? Industry { get; init; }

    /// <summary>僅列待我核決（輪到我的步驟）的合約。</summary>
    public bool PendingMyDecision { get; init; }
}

public sealed record ContractSearchRequest : PagedQueryParameters<ContractSearchFilter>;

/// <summary>合約條款（建立與更新共用）。</summary>
public sealed record ContractTermsDto(
    string Title, string CustomerName, string? CustomerContactName, string? CustomerContactEmail,
    ContractType Type, ContractIndustry Industry, decimal TotalAmount,
    DateOnly? QuoteDate, DateOnly? StartDate, DateOnly? DeliveryDate,
    bool OpenEndedScope, int? ReviewDeadlineWorkdays, int? MaxReviewRounds, bool DeemedPassOnTimeout,
    IpRule IpRule, ScanRule ScanRule, int? DeemedAcceptanceWorkdays, bool FinalPaymentHeldForMinorDefects,
    LiabilityCapType LiabilityCap, decimal? LiabilityCapAmount, bool ExcludesIndirectLoss,
    int WarrantyMonths, int? IncidentNotifyHours, int? AuditNoticeDays, int? AuditPerYear, bool AuditCostOnVendor,
    string ServiceHours, string? SupportChannel, string? Notes, int CoopNoticeWorkdays = 3);

public sealed record ContractListDto(
    Guid Id, string ContractNo, string Title, string CustomerName, ContractType Type, ContractIndustry Industry, ContractStatus Status,
    decimal TotalAmount, DateOnly? DeliveryDate, DateOnly? SignedDate, int ProjectCount,
    IReadOnlyList<ContractMemberRole> MyRoles, ContractFlagSeverity? WorstFlag, ContractApprovalStep? NextStep, bool PendingMyDecision,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);

public sealed record ContractMemberDto(Guid UserId, ContractMemberRole Role);

public sealed record ContractFlagDto(string ItemCode, ContractFlagSeverity Severity, string Message);

/// <param name="ClientTrap">規範列出的甲方常見陷阱。</param>
/// <param name="Defense">乙方談判底線與防禦對策。</param>
public sealed record ContractReviewItemDto(
    string Code, string Name, string ClientTrap, string Defense, string? ClientClause, ReviewStance Stance, ReviewResult Result,
    string? Note, Guid? ReviewedBy, DateTimeOffset? ReviewedAt);

public sealed record ContractApprovalDto(
    int Cycle, ContractApprovalStep Step, ApprovalDecision Decision, string? Comment, Guid DecidedBy, string DecidedByName, DateTimeOffset DecidedAt);

public sealed record ContractDeliverableItemDto(Guid Id, int SortOrder, string Stage, string Name, string Format, string AcceptMethod, bool RequiresCustomerSignoff);

public sealed record ContractSlaLevelDto(Guid Id, int Level, string Name, string Definition, decimal ResponseHours, string Target);

public sealed record ContractProjectDto(Guid ProjectId, string ProjectCode, string ProjectName, ProjectStatus ProjectStatus, string BoundBy, DateTimeOffset BoundAtUtc);

public sealed record ContractAttachmentDto(
    Guid Id, string FileName, string OriginalFileName, string? ContentType, long FileSize, ContractAttachmentKind Kind, bool CanPreview,
    string CreatedBy, DateTimeOffset CreatedAtUtc);

public sealed record ContractEventDto(ContractEventType Type, string Message, string ActorName, DateTimeOffset At);

/// <param name="CanEditTerms">可編修條款（簽約前三階段，且為業務或合約 PM）。</param>
/// <param name="CanManage">業務或合約 PM：維護成員、附件、送審、簽約登錄、綁定專案。</param>
/// <param name="CanReviewItems">可填審閱結果（合約 PM 或法務，且條款可編修）。</param>
/// <param name="CanSubmit">可送審（可編修、且為業務或合約 PM）。</param>
/// <param name="CanWithdraw">待核決時，業務或合約 PM 可撤回送審。</param>
/// <param name="CanDecide">目前輪到我核決（含 PM 初審、法務、高階主管，職責分離後）。</param>
/// <param name="DecideBlockedReason">我擁有目前待決步驟的角色，卻因職責分離等原因不能核決時的說明（否則為 null）。</param>
/// <param name="DecideStep">我可以做的核決步驟。</param>
/// <param name="CanSign">核決齊備，可登錄簽約。</param>
/// <param name="AllowedStatuses">可手動切換的狀態。</param>
public sealed record ContractActionsDto(
    bool CanEditTerms, bool CanManage, bool CanReviewItems, bool CanSubmit, bool CanWithdraw, bool CanDecide, ContractApprovalStep? DecideStep, string? DecideBlockedReason, bool CanSign,
    bool CanBindProjects, bool CanEditAppendix, bool CanUpload, IReadOnlyList<ContractStatus> AllowedStatuses);

public sealed record ContractDto(
    Guid Id, string ContractNo, ContractStatus Status, ContractTermsDto Terms, DateOnly? SignedDate, int ApprovalCycle,
    DateTimeOffset? SubmittedAt, Guid? SubmittedBy, string? SnapshotHash,
    IReadOnlyList<ContractMemberDto> Members, IReadOnlyList<ContractReviewItemDto> ReviewItems, IReadOnlyList<ContractFlagDto> Flags,
    IReadOnlyList<ContractApprovalDto> Approvals, IReadOnlyList<ContractApprovalStep> RequiredSteps, ContractApprovalStep? NextStep,
    IReadOnlyList<ContractDeliverableItemDto> DeliverableItems, IReadOnlyList<ContractSlaLevelDto> SlaLevels,
    IReadOnlyList<ContractProjectDto> Projects, IReadOnlyList<ContractAttachmentDto> Attachments, IReadOnlyList<ContractEventDto> Events,
    ContractActionsDto Actions, string CreatedBy, DateTimeOffset CreatedAtUtc, string RowVersion);

public sealed record CreateContractRequest(ContractTermsDto Terms);

public sealed record UpdateContractRequest(ContractTermsDto Terms, string RowVersion);

public sealed record ContractMemberAssignment(Guid UserId, ContractMemberRole Role);

/// <summary>整組取代成員（每個角色最多 5 人；至少要有一位業務或合約 PM）。</summary>
public sealed record SetContractMembersRequest(IReadOnlyList<ContractMemberAssignment> Members);

public sealed record UpdateReviewItemRequest(string? ClientClause, ReviewStance Stance, ReviewResult Result, string? Note);

public sealed record ChangeContractStatusRequest(ContractStatus Status, string? Reason);

public sealed record ContractDecisionRequest(ApprovalDecision Decision, string? Comment);

/// <param name="Step">僅 Pm 或 Legal；高階主管走 executive-decision。</param>
public sealed record ContractReviewDecisionRequest(ContractApprovalStep Step, ApprovalDecision Decision, string? Comment);

public sealed record SignContractRequest(DateOnly SignedDate, DateOnly? StartDate, string RowVersion);

public sealed record ContractDeliverableItemRequest(string Stage, string Name, string Format, string AcceptMethod, bool RequiresCustomerSignoff);

public sealed record UpdateDeliverableListRequest(IReadOnlyList<ContractDeliverableItemRequest> Items);

public sealed record ContractSlaLevelRequest(int Level, string Name, string Definition, decimal ResponseHours, string Target);

public sealed record UpdateSlaLevelsRequest(IReadOnlyList<ContractSlaLevelRequest> Levels);

public sealed record BindContractProjectRequest(Guid ProjectId);
