using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Contracts;

// ===== 非服務時間支援與加班計費 =====

public sealed record OvertimeSegmentDto(decimal Multiplier, string Name, decimal Hours);

/// <summary>非服務時間支援的合約參數：提前申請工作日、單次窗口上限、三種加班倍率。</summary>
public sealed record SupportSettingsDto(int AdvanceWorkdays, int MaxWindowDays, decimal WeekdayRate, decimal NightRestRate, decimal HolidayRate, bool RequiresApproval = true,
    string? LastReason = null, string? LastUpdatedBy = null, DateTimeOffset? LastUpdatedAt = null);

public sealed record UpdateSupportSettingsRequest(
    int AdvanceWorkdays, int MaxWindowDays, decimal WeekdayRate, decimal NightRestRate, decimal HolidayRate, string? Reason, bool RequiresApproval = true);

/// <param name="EarliestStartDate">依「提前 5 個工作日」規則，最早可支援的日期。</param>
public sealed record OvertimeEstimateDto(
    decimal TotalHours, decimal OvertimeHours, decimal WeightedHours, IReadOnlyList<OvertimeSegmentDto> Segments, DateOnly EarliestStartDate, string? Problem);

public sealed record SupportRequestDto(
    Guid Id, int Number, string Title, DateTimeOffset WindowStart, DateTimeOffset WindowEnd, SupportStatus Status,
    decimal EstimatedHours, decimal EstimatedOvertimeHours, decimal EstimatedWeightedHours, IReadOnlyList<OvertimeSegmentDto> Segments,
    DateTimeOffset? ActualStart, DateTimeOffset? ActualEnd, decimal? ActualOvertimeHours, decimal? ActualWeightedHours,
    string RequestedByName, DateTimeOffset RequestedAt, string? DecidedByName, DateTimeOffset? DecidedAt, string? Note,
    bool CanDecide, bool CanComplete, bool CanCancel, string RowVersion,
    DateOnly? ClientRequestedDate = null, string? ClientRequestChannel = null, string? ClientRequestRef = null);

public sealed record SupportOverviewDto(
    IReadOnlyList<SupportRequestDto> Items, decimal ApprovedWeightedHours, decimal CompletedWeightedHours, bool CanCreate, bool Active, DateOnly EarliestStartDate,
    SupportSettingsDto Settings, bool CanEditSettings);

public sealed record EstimateSupportRequest(DateTimeOffset Start, DateTimeOffset End);

public sealed record CreateSupportRequest(
    string Title, DateTimeOffset Start, DateTimeOffset End, string? Note,
    DateOnly? ClientRequestedDate = null, string? ClientRequestChannel = null, string? ClientRequestRef = null, string? EvidenceFileName = null);

public sealed record DecideSupportRequest(bool Approve, string? Note);

public sealed record CompleteSupportRequest(DateTimeOffset ActualStart, DateTimeOffset ActualEnd, string? Note);

public sealed record CancelSupportRequest(string? Note);

/// <summary>簽約後調整附件二各等級的處理期限（時鐘小時或工作日擇一，皆空＝沿用系統預設）。</summary>
public sealed record SlaDueItem(int Level, decimal? DueHours, int? DueWorkdays, decimal? ResponseHours = null);

public sealed record UpdateSlaDueRequest(
    IReadOnlyList<SlaDueItem> Levels, string? Reason, DateOnly? AgreementDate = null, string? AgreementChannel = null, string? AgreementRef = null, string? EvidenceFileName = null);

/// <summary>記錄（或更正、取消）附件一項目的實際交付；日期為空代表取消交付記錄。</summary>
public sealed record MarkDeliveredRequest(DateOnly? DeliveredDate, string? Note, string? Channel = null, string? EvidenceFileName = null);

public sealed record DeliverableDateItem(Guid ItemId, DateOnly? DeliveryDate);

/// <summary>簽約後調整附件一的指定交付日：須雙方協議佐證、原因，並留事件紀錄。</summary>
public sealed record UpdateDeliverableDatesRequest(
    IReadOnlyList<DeliverableDateItem> Items, string? Reason, DateOnly? AgreementDate = null, string? AgreementChannel = null, string? AgreementRef = null,
    string? EvidenceFileName = null);

// ===== 儀表 =====

public sealed record DashboardAlertDto(Guid ContractId, string ContractNo, string Title, string Message, DateOnly? Date, string Severity);

public sealed record ContractDashboardDto(
    int ActiveCount, decimal ActiveAmount, int PendingMyDecision, int CoopOverdue, int CoopNoticeOverdue, int WarrantyExpiring, int InvoiceableCount,
    decimal InvoiceableAmount, int AcceptanceSoon, IReadOnlyList<DashboardAlertDto> Alerts, int DeliverablesOverdue = 0);

// ===== 談判話術與啟動會議 =====

public sealed record PlaybookScenarioDto(string Title, string Challenge, string Script);

public sealed record PlaybookSlideDto(string Title, IReadOnlyList<string> Points, string KeyMessage);

public sealed record PlaybookIndustryDto(string Industry, IReadOnlyList<ComplianceGuideDto> Items);

public sealed record ComplianceGuideDto(string Name, string Risk, string Defense);

public sealed record PlaybookDto(
    IReadOnlyList<PlaybookScenarioDto> Scenarios, IReadOnlyList<PlaybookSlideDto> KickoffSlides, IReadOnlyList<PlaybookIndustryDto> Industries,
    IReadOnlyList<string> WarrantyExclusions, IReadOnlyList<string> ReviewPrinciples);
