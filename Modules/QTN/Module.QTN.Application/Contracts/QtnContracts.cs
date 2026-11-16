using SGS.Modules.QTN.Application.Rules;
using SGS.Modules.QTN.Infrastructure.Entities;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.QTN.Application.Contracts;

/// <summary>QTN 業務錯誤（對應 HTTP 狀態與 code）。</summary>
public sealed record QtnError(string Code, int Status, string Title, string Detail)
{
    public static QtnError Validation(string detail, string code = "QTN.VALIDATION_FAILED") => new(code, 400, "Invalid Request", detail);

    /// <summary>找不到或無權限可見：一律 404，避免洩漏資源是否存在。</summary>
    public static QtnError NotFound(string detail = "資源不存在。") => new("QTN.NOT_FOUND", 404, "Resource Not Found", detail);

    public static QtnError Forbidden(string detail) => new("QTN.FORBIDDEN", 403, "Forbidden", detail);

    public static QtnError Conflict(string code, string detail) => new(code, 409, "Conflict", detail);

    public static QtnError Concurrency() => Conflict("QTN.CONCURRENCY_CONFLICT", "資料已被他人修改，請重新整理後再試。");

    public static QtnError FromRule(RuleResult rule) => rule.Outcome switch
    {
        RuleOutcome.Locked => Conflict("QTN.CONTENT_LOCKED", rule.Detail ?? "內容已鎖定。"),
        RuleOutcome.InvalidTransition => Conflict("QTN.INVALID_TRANSITION", rule.Detail ?? "狀態不允許此操作。"),
        RuleOutcome.RoleNotAllowed => Forbidden(rule.Detail ?? "權限不足。"),
        RuleOutcome.SeparationOfDuties => new("QTN.SEPARATION_OF_DUTIES", 403, "Forbidden", rule.Detail ?? "違反職責分離。"),
        RuleOutcome.Blocked => Conflict("QTN.MARGIN_LOCKED", rule.Detail ?? "已鎖定送出。"),
        _ => Validation(rule.Detail ?? "資料不正確。")
    };
}

/// <summary>操作結果（Result Pattern）。</summary>
public sealed class QtnResult<T>
{
    private QtnResult(T? value, QtnError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public QtnError? Error { get; }

    public bool IsSuccess => Error is null;

    public static QtnResult<T> Success(T value) => new(value, null);

    public static QtnResult<T> Failure(QtnError error) => new(default, error);

    public static implicit operator QtnResult<T>(QtnError error) => Failure(error);
}

/// <summary>呼叫者身分與權限（由控制器依權限碼解析後傳入；欄位級權限於後端投影）。</summary>
/// <param name="CanEditEstimate">QTN.ESTIMATE.EDIT：可編修 WBS 與三點工時、執行技術複核。</param>
/// <param name="CanEditPrice">QTN.QUOTE.EDIT：可編修折扣、直接費用、付款里程碑與條款（另須具 QTN.PRICE.READ）。</param>
public sealed record QtnCaller(Guid UserId, string Name, string? IpAddress, QuoteFieldAccess Access, bool CanEditEstimate, bool CanEditPrice);

/// <summary>非分頁清單查詢（主檔）。</summary>
public sealed record ListRequest(bool IncludeInactive = false);

// ===== 客戶 =====

public sealed record CustomerFilter
{
    public bool? IsActive { get; init; }
}

public sealed record CustomerSearchRequest : PagedQueryParameters<CustomerFilter>;

public sealed record CustomerListDto(Guid Id, string Code, string Name, string? TaxId, string? Industry, bool IsStrategic, bool IsActive);

public sealed record CustomerDto(
    Guid Id, string Code, string Name, string? TaxId, string? Industry, string? Address, string? ContactName, string? ContactEmail, string? ContactPhone,
    bool IsStrategic, bool IsActive, string? Notes, string RowVersion);

public sealed record UpsertCustomerRequest(
    string Code, string Name, string? TaxId, string? Industry, string? Address, string? ContactName, string? ContactEmail, string? ContactPhone,
    bool IsStrategic, bool IsActive, string? Notes, string? RowVersion = null);

// ===== 角色費率 =====

/// <param name="CostRate">內部成本單價：無 QTN.COST.READ 時為 null。</param>
public sealed record RateRoleDto(Guid Id, string Code, string Name, decimal? CostRate, decimal ListRate, int SortOrder, bool IsActive, string RowVersion);

/// <param name="CostRate">建立時必填；更新時 null＝不變。</param>
/// <param name="Reason">費率調整原因（寫入歷程）。</param>
public sealed record UpsertRateRoleRequest(
    string Code, string Name, decimal? CostRate, decimal ListRate, int SortOrder, bool IsActive, string? Reason, string? RowVersion = null);

public sealed record RateRoleHistoryDto(DateTimeOffset EffectiveAt, decimal? CostRate, decimal ListRate, string? Reason, string ChangedBy);

// ===== 標準 WBS 工時庫 =====

public sealed record WbsTemplateListDto(Guid Id, string Code, string Name, string? Description, bool IsActive, int ItemCount, decimal ExpectedDays);

public sealed record WbsTemplateItemDto(
    Guid Id, int SortOrder, string Name, string? Deliverable, WbsPhase Phase, Guid RateRoleId, string RoleName,
    decimal Optimistic, decimal MostLikely, decimal Pessimistic, decimal ExpectedDays, int BaselineVersion, DateTimeOffset? LastCalibratedAt);

public sealed record WbsTemplateDto(
    Guid Id, string Code, string Name, string? Description, bool IsActive, IReadOnlyList<WbsTemplateItemDto> Items, string RowVersion);

public sealed record WbsTemplateItemInput(
    Guid? Id, string Name, string? Deliverable, WbsPhase Phase, Guid RateRoleId, decimal Optimistic, decimal MostLikely, decimal Pessimistic);

public sealed record UpsertWbsTemplateRequest(
    string Code, string Name, string? Description, bool IsActive, IReadOnlyList<WbsTemplateItemInput> Items, string? RowVersion = null);

// ===== 審批人 =====

public sealed record ApproverRoleDto(ApprovalRole Role, string RoleName, IReadOnlyList<Guid> UserIds);

public sealed record SetApproversRequest(ApprovalRole Role, IReadOnlyList<Guid> UserIds);

// ===== 商機 =====

public sealed record OpportunityFilter
{
    public IReadOnlyList<OpportunityStatus>? Status { get; init; }
    public Guid? CustomerId { get; init; }
    public IReadOnlyList<PricingModel>? PricingModel { get; init; }
}

public sealed record OpportunitySearchRequest : PagedQueryParameters<OpportunityFilter>;

public sealed record OpportunityMemberDto(Guid UserId, OpportunityRole Role);

public sealed record QuoteSummaryDto(
    Guid Id, string QuoteNo, string VersionLabel, bool IsChangeRequest, QuoteStatus Status, PricingModel PricingModel, decimal TotalQuotedDays,
    decimal? FinalPrice, MarginLight? MarginLight, DateOnly? ValidUntil, DateTimeOffset CreatedAtUtc);

public sealed record OpportunityListDto(
    Guid Id, string OpportunityNo, string Title, string CustomerName, ProjectCategory Category, PricingModel PricingModel, OpportunityStatus Status,
    QuoteSummaryDto? LatestQuote, IReadOnlyList<OpportunityRole> MyRoles, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);

public sealed record OpportunityDto(
    Guid Id, string OpportunityNo, string? CrmRef, Guid CustomerId, string CustomerName, bool CustomerIsStrategic, string Title,
    ProjectCategory Category, PricingModel PricingModel, OpportunityStatus Status, string? Scope, DateOnly? ExpectedGoLive, int? ConcurrentUsers,
    string? SecurityLevel, string? NonFunctional, bool IpBuyout, IReadOnlyList<OpportunityMemberDto> Members, IReadOnlyList<QuoteSummaryDto> Quotes,
    string StrategyDefense, bool CanManage, bool CanCreateQuote, string RowVersion);

public sealed record UpsertOpportunityRequest(
    Guid CustomerId, string Title, string? CrmRef, ProjectCategory Category, PricingModel PricingModel, string? Scope, DateOnly? ExpectedGoLive,
    int? ConcurrentUsers, string? SecurityLevel, string? NonFunctional, bool IpBuyout, string? RowVersion = null);

public sealed record SetOpportunityMembersRequest(IReadOnlyList<OpportunityMemberDto> Members);

// ===== 報價版本 =====

public sealed record QuoteFilter
{
    public IReadOnlyList<QuoteStatus>? Status { get; init; }
    public IReadOnlyList<PricingModel>? PricingModel { get; init; }
    public IReadOnlyList<MarginLight>? MarginLight { get; init; }

    /// <summary>僅列輪到我審批的報價。</summary>
    public bool PendingMyApproval { get; init; }

    /// <summary>僅列各商機最新版本。</summary>
    public bool LatestOnly { get; init; }
}

public sealed record QuoteSearchRequest : PagedQueryParameters<QuoteFilter>;

public sealed record QuoteListDto(
    Guid Id, Guid OpportunityId, string OpportunityNo, string Title, string CustomerName, string QuoteNo, string VersionLabel, bool IsChangeRequest,
    QuoteStatus Status, PricingModel PricingModel, decimal TotalQuotedDays, decimal? FinalPrice, MarginLight? MarginLight,
    ApprovalRole? PendingRole, bool PendingMyApproval, DateOnly? ValidUntil, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc);

/// <summary>報價金額（依欄位級權限投影：無權者為 null）。</summary>
public sealed record QuoteTotalsDto(
    decimal TotalQuotedDays, decimal TotalExpectedDays, decimal RiskFactor,
    decimal? LaborCost, decimal? Overhead, decimal? TotalCost, decimal? MarginRate,
    decimal? DirectExpense, decimal? LaborListTotal, decimal? IpLicenseFee, decimal? ListTotal, decimal? DiscountAmount,
    decimal? FinalPrice, decimal TaxRate, decimal? TaxAmount, decimal? FinalPriceWithTax, decimal? PeriodListPrice,
    MarginLight? MarginLight, decimal? BonusFactor);

/// <summary>計價模式專屬參數。</summary>
public sealed record QuoteTermsDto(
    int? SprintCount, int? RetainerMonths, DateOnly? RetainerStart, decimal? RetainerMonthlyDaysCap, decimal? OverageDailyRate,
    decimal? OffHoursMultiplier, int? DurationWeeks, int ValidDays, decimal TaxRate);

public sealed record AllocationDto(
    Guid Id, Guid RateRoleId, string RoleName, decimal Optimistic, decimal MostLikely, decimal Pessimistic, decimal ExpectedDays, decimal QuotedDays,
    decimal? SnapshotCostRate, decimal? SnapshotListRate, decimal? CostAmount, decimal? ListAmount);

public sealed record WbsItemDto(
    Guid Id, Guid? ParentId, int SortOrder, string WbsCode, string Name, string? Deliverable, WbsPhase Phase, int Stage, decimal KTech, decimal KReq,
    Guid? TemplateItemId, IReadOnlyList<AllocationDto> Allocations, decimal QuotedDays, decimal? ListAmount);

public sealed record ExpenseDto(Guid Id, ExpenseCategory Category, string Description, decimal? Amount, bool IsCapped);

public sealed record MilestoneDto(int Seq, string Name, decimal Percent, decimal? Amount, decimal? AmountWithTax, string Trigger);

public sealed record ClauseDto(Guid Id, int SortOrder, string Code, string Title, string Body);

public sealed record SlaLevelDto(string Level, string Definition, string ResponseTime, string ResolveTarget);

public sealed record ApprovalStepDto(
    int Cycle, int Seq, ApprovalRole Role, string RoleName, ApprovalStepState State, Guid? DecidedBy, string? DecidedByName, DateTimeOffset? DecidedAt, string? Comment);

public sealed record QuoteLogDto(QuoteLogAction Action, ApprovalRole? Role, int Cycle, string ActorName, string? IpAddress, DateTimeOffset At, string? Message);

public sealed record QuoteAttachmentDto(
    Guid Id, QuoteAttachmentKind Kind, string FileName, string OriginalFileName, string? ContentType, long FileSize, bool CanPreview,
    string CreatedBy, DateTimeOffset CreatedAtUtc);

/// <param name="Route">依目前金額與毛利試算的必經審批路徑（僅可見價格者）。</param>
public sealed record QuoteEvaluationDto(
    IReadOnlyList<PhaseRatioDto> PhaseRatios, IReadOnlyList<string> Warnings, IReadOnlyList<string> BlockingErrors,
    IReadOnlyList<ApprovalRole>? Route, bool RequiresDiscountExplanation);

/// <param name="DecideRole">輪到我審批的關卡角色。</param>
/// <param name="DecideBlockedReason">我擔任目前關卡角色卻因職責分離不能審批時的說明。</param>
public sealed record QuoteActionsDto(
    bool CanEditEstimate, bool CanEditPrice, bool CanTechReview, bool CanSubmit, bool CanDecide, ApprovalRole? DecideRole, string? DecideBlockedReason,
    bool CanRevise, bool CanIssue, bool CanMarkOutcome, bool CanCreateChangeRequest, bool CanConvertToContract, bool CanExportCustomerPdf,
    bool CanExportMarginReport, bool CanUpload);

public sealed record QuoteDto(
    Guid Id, Guid OpportunityId, string OpportunityNo, string OpportunityTitle, string CustomerName, ProjectCategory Category, bool IpBuyout,
    string QuoteNo, string VersionLabel, bool IsChangeRequest, Guid? ParentVersionId, QuoteStatus Status, PricingModel PricingModel, string Title,
    bool IsLocked, Guid? TechReviewedBy, DateTimeOffset? TechReviewedAt, string? TechReviewNote,
    QuoteTotalsDto Totals, string? DiscountReason, bool SpecialApproval, string? SpecialApprovalReason, QuoteTermsDto Terms,
    DateOnly? IssuedDate, DateOnly? ValidUntil, int ApprovalCycle, string? ContentHash, Guid? SubmittedBy, DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt, string? OutcomeNote, Guid? PmsContractId, string? PmsContractNo, string? Notes,
    IReadOnlyList<WbsItemDto> Wbs, IReadOnlyList<ExpenseDto> Expenses, IReadOnlyList<MilestoneDto> Milestones, IReadOnlyList<ClauseDto> Clauses,
    IReadOnlyList<SlaLevelDto> SlaLevels, QuoteEvaluationDto Evaluation, IReadOnlyList<ApprovalStepDto> ApprovalSteps, IReadOnlyList<QuoteLogDto> Logs,
    IReadOnlyList<QuoteAttachmentDto> Attachments, QuoteFieldAccess Access, QuoteActionsDto Actions, string RowVersion);

// ----- 報價編修（草稿；一次儲存，null 區段＝不變） -----

public sealed record AllocationInput(Guid RateRoleId, decimal Optimistic, decimal MostLikely, decimal Pessimistic);

/// <param name="Key">前端暫用鍵（新節點可用任意字串，既有節點可用 Id 字串）。</param>
/// <param name="ParentKey">上層節點的 Key（null＝第一層）。</param>
public sealed record WbsItemInput(
    string Key, string? ParentKey, string Name, string? Deliverable, WbsPhase Phase, int Stage, decimal KTech, decimal KReq, Guid? TemplateItemId,
    IReadOnlyList<AllocationInput>? Allocations);

public sealed record ExpenseInput(ExpenseCategory Category, string Description, decimal Amount, bool IsCapped);

public sealed record MilestoneInput(string Name, decimal Percent, string? Trigger);

public sealed record ClauseInput(string Code, string Title, string Body);

/// <param name="Wbs">WBS 樹（整棵取代；須具 QTN.ESTIMATE.EDIT）。</param>
/// <param name="Expenses">直接費用（整批取代；須具價格編修權）。</param>
/// <param name="Milestones">付款里程碑（整批取代）；ResetMilestones＝true 時依計價模式重設預設值。</param>
public sealed record QuoteEditRequest(
    string RowVersion,
    string? Title = null,
    QuoteTermsDto? Terms = null,
    decimal? DiscountAmount = null,
    string? DiscountReason = null,
    decimal? IpLicenseFee = null,
    bool? SpecialApproval = null,
    string? SpecialApprovalReason = null,
    string? Notes = null,
    IReadOnlyList<WbsItemInput>? Wbs = null,
    IReadOnlyList<ExpenseInput>? Expenses = null,
    IReadOnlyList<MilestoneInput>? Milestones = null,
    bool ResetMilestones = false,
    IReadOnlyList<ClauseInput>? Clauses = null,
    IReadOnlyList<SlaLevelDto>? SlaLevels = null);

public sealed record CreateQuoteRequest(string? Title, IReadOnlyList<Guid>? TemplateIds);

public sealed record ImportTemplateRequest(Guid TemplateId, string RowVersion);

public sealed record TechReviewRequest(string? Note, string RowVersion);

public sealed record SubmitQuoteRequest(string RowVersion, string? Comment);

public sealed record DecideQuoteRequest(bool Approve, string? Comment, string RowVersion);

/// <param name="RefreshRates">true＝新版本改用目前角色費率（否則沿用來源版本快照）。</param>
public sealed record ReviseQuoteRequest(string Reason, bool RefreshRates = false);

public sealed record IssueQuoteRequest(DateOnly? IssuedDate, string RowVersion);

public sealed record QuoteOutcomeRequest(bool Won, string? Note, string RowVersion);

public sealed record ConvertToContractRequest(Guid? PmUserId, DateOnly? StartDate, DateOnly? DeliveryDate, int WarrantyMonths = 6);

public sealed record ConvertToContractResultDto(Guid ContractId, string ContractNo);

public sealed record QuotePdfFile(byte[] Content, string FileName);

public sealed record QuoteFileView(byte[] Content, string ContentType);

public sealed record ScanResultDto(int Processed, int Notified, string Message);

// ===== CR 變更評估 =====

public sealed record ChangeAssessmentFilter
{
    public IReadOnlyList<ChangeAssessmentStatus>? Status { get; init; }
    public Guid? OpportunityId { get; init; }
    public bool OverdueOnly { get; init; }
}

public sealed record ChangeAssessmentSearchRequest : PagedQueryParameters<ChangeAssessmentFilter>;

public sealed record ChangeAssessmentDto(
    Guid Id, string AssessmentNo, Guid OpportunityId, string OpportunityNo, string CustomerName, Guid BaseQuoteVersionId, string BaseVersionLabel,
    Guid? CrQuoteVersionId, string? CrVersionLabel, QuoteStatus? CrQuoteStatus, ChangeRequester Requester, string RequesterName,
    DateOnly ReceivedDate, DateOnly AssessmentDueDate, bool IsOverdue, string Title, string OriginalSpec, string ChangedSpec,
    decimal NewDevDays, decimal ReworkDays, decimal RegressionDays, decimal PmDays, decimal TotalDays,
    decimal? AmountUntaxed, decimal? AmountWithTax, int DelayWorkdays, ChangeAssessmentStatus Status, string? CustomerSignatory,
    DateOnly? CustomerSignedDate, bool CanEdit, string RowVersion);

public sealed record CreateChangeAssessmentRequest(
    Guid BaseQuoteVersionId, ChangeRequester Requester, string RequesterName, DateOnly ReceivedDate, string Title, string OriginalSpec, string ChangedSpec);

public sealed record UpdateChangeAssessmentRequest(
    string Title, string OriginalSpec, string ChangedSpec, decimal NewDevDays, decimal ReworkDays, decimal RegressionDays, decimal PmDays,
    int DelayWorkdays, string RowVersion);

/// <summary>以評估工時衍生 CR 報價版本：各類工時以指定角色計價（費率沿用原報價快照，無則取目前費率）。</summary>
public sealed record CreateCrQuoteRequest(Guid DevRoleId, Guid QaRoleId, Guid PmRoleId, string RowVersion);

public sealed record CustomerSignChangeRequest(string Signatory, DateOnly SignedDate, string RowVersion);

// ===== 結案復盤 =====

public sealed record VarianceReviewFilter
{
    public IReadOnlyList<VarianceReviewStatus>? Status { get; init; }
}

public sealed record VarianceReviewSearchRequest : PagedQueryParameters<VarianceReviewFilter>;

public sealed record VarianceItemDto(
    Guid Id, string WbsCode, string Name, Guid? TemplateItemId, decimal QuotedDays, decimal? ActualDays, decimal? VarianceRate, bool ExceedsThreshold,
    string? RootCause, bool Calibrated);

public sealed record VarianceReviewDto(
    Guid Id, Guid QuoteVersionId, string QuoteNo, string VersionLabel, string OpportunityTitle, string CustomerName, DateOnly ClosedDate,
    DateOnly ReportDueDate, bool IsOverdue, VarianceReviewStatus Status, string? RetrospectiveNotes, DateOnly? CalibrationDueDate,
    decimal TotalQuotedDays, decimal? TotalActualDays, decimal? TotalVarianceRate, int ExceedCount, IReadOnlyList<VarianceItemDto> Items,
    bool CanEdit, string RowVersion);

public sealed record CreateVarianceReviewRequest(Guid QuoteVersionId, DateOnly ClosedDate);

public sealed record VarianceItemInput(Guid Id, decimal? ActualDays, string? RootCause);

public sealed record UpdateVarianceReviewRequest(IReadOnlyList<VarianceItemInput> Items, string? RetrospectiveNotes, string RowVersion);

public sealed record CalibrateRequest(IReadOnlyList<Guid> ItemIds, string RowVersion);

// ===== 儀表板 =====

public sealed record DashboardRequest(int? Year);

public sealed record CountByDto(string Key, int Count, decimal? Amount);

public sealed record QuoteDashboardDto(
    int Year, int OpenOpportunities, int PendingMyApproval, int QuotesInApproval, int Issued, int Won, int Lost, decimal? WinRate,
    decimal? PipelineAmount, decimal? WonAmount, decimal? AverageMarginRate, IReadOnlyList<CountByDto> ByStatus, IReadOnlyList<CountByDto> ByLight,
    IReadOnlyList<CountByDto> ByPricingModel, int OverdueAssessments, int OpenVarianceReviews, int ExpiringIn7Days);
