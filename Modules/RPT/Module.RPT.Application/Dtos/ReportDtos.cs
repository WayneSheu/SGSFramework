using SGS.Modules.RPT.Infrastructure.Domain;
using SGS.Modules.RPT.Infrastructure.Entities;
using SGSFramework.Core.Abstractions.Entities.ReportCategories;
using SGSFramework.Core.Abstractions.Permissions.Entities;

namespace SGS.Modules.RPT.Application.Dtos;

/// <summary>報告清單分類</summary>
public enum ReportListBox
{
    /// <summary>全部可見報告</summary>
    All = 0,

    /// <summary>指派給我的報告</summary>
    MyWork = 1,

    /// <summary>待我審核</summary>
    AwaitingReview = 2,

    /// <summary>待我簽署</summary>
    AwaitingSign = 3
}

public sealed record ReportListQuery(
    ReportListBox Box = ReportListBox.All,
    ReportLifecycleStatus? Status = null,
    string? CategoryCode = null,
    string? Keyword = null);

public sealed record ReportListItemDto(
    Guid Id,
    string ReportNo,
    int Version,
    string CategoryCode,
    string Title,
    string SampleNo,
    string? CustomerName,
    ConfidentialityLevel Confidentiality,
    bool IsBlind,
    ReportLifecycleStatus Status,
    string AuthorName,
    string? ReviewerName,
    string? SignerName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SignedAt);

public sealed record ReportResultDto(
    Guid Id,
    int Sequence,
    string ItemCode,
    string ItemName,
    string? Value,
    string? Unit,
    string? LimitText,
    string? Method,
    ReportValueSource ValueSource,
    string? InstrumentValue,
    string? ChangeReason,
    string? Remark);

public sealed record ReportAssignmentDto(Guid UserId, string UserName, ReportAssignmentRole Role);

/// <summary>使用者對此報告目前可執行的操作（依授權與狀態計算）</summary>
public sealed record ReportPermissionsDto(
    bool CanEdit,
    bool CanDownload,
    bool CanRequestVoid,
    bool CanViewAudit,
    bool CanSeeBlindIdentity,
    IReadOnlyList<ReportTransition> AvailableTransitions);

public sealed record ReportDetailDto(
    Guid Id,
    string ReportNo,
    int Version,
    Guid? SupersedesReportId,
    string CategoryCode,
    string? CategoryName,
    Guid TenantLabId,
    string? LabName,
    string Title,
    string? CustomerCode,
    string? CustomerName,
    string SampleNo,
    string? SampleDescription,
    ConfidentialityLevel Confidentiality,
    bool IsBlind,
    ReportLifecycleStatus Status,
    Guid AuthorId,
    string AuthorName,
    DateTimeOffset? SubmittedAt,
    string? ReviewerName,
    DateTimeOffset? ReviewedAt,
    string? SignerName,
    string? SignerCertificateNo,
    DateTimeOffset? SignedAt,
    string? PdfSha256,
    string? VerificationToken,
    DateTimeOffset? VoidedAt,
    string? VoidReason,
    string RowVersion,
    IReadOnlyList<ReportResultDto> Results,
    IReadOnlyList<ReportAssignmentDto> Assignments,
    ReportPermissionsDto Permissions);

public sealed record CreateReportRequest(
    string CategoryCode,
    string Title,
    string SampleNo,
    string? SampleDescription,
    string? CustomerCode,
    string? CustomerName,
    ConfidentialityLevel? Confidentiality,
    bool IsBlind);

public sealed record UpdateReportHeaderRequest(
    string Title,
    string SampleNo,
    string? SampleDescription,
    string? CustomerCode,
    string? CustomerName,
    ConfidentialityLevel Confidentiality,
    bool IsBlind,
    string? RowVersion);

public sealed record SaveReportResultItem(
    Guid? Id,
    string ItemCode,
    string ItemName,
    string? Value,
    string? Unit,
    string? LimitText,
    string? Method,
    ReportValueSource ValueSource,
    string? ChangeReason,
    string? Remark);

public sealed record SaveReportResultsRequest(List<SaveReportResultItem> Items, string? RowVersion);

public sealed record SetReportAssignmentsRequest(List<ReportAssignmentInput> Assignments);

public sealed record ReportAssignmentInput(Guid UserId, ReportAssignmentRole Role);

public sealed record ReportTransitionRequest(ReportTransition Transition, string? Reason, string? RowVersion);

public sealed record RequestReportVoidRequest(string Reason, bool Reissue);

public sealed record ReportStatusHistoryDto(
    ReportLifecycleStatus? FromStatus,
    ReportLifecycleStatus ToStatus,
    string Action,
    string ActorName,
    string? Reason,
    DateTimeOffset OccurredAt);

public sealed record ReportAccessLogDto(
    string Action,
    string UserName,
    string? RemoteIp,
    string? Detail,
    DateTimeOffset OccurredAt);

public sealed record ReportVoidRequestDto(
    Guid Id,
    Guid ReportId,
    string ReportNo,
    string CategoryCode,
    string Reason,
    bool Reissue,
    RoleChangeRequestStatus Status,
    string RequestedByName,
    DateTimeOffset RequestedAt,
    string? ReviewedByName,
    DateTimeOffset? ReviewedAt,
    string? ReviewComment,
    Guid? ReissuedReportId,
    bool CanApprove,
    bool IsMine);

/// <summary>公開真偽查驗結果（不含客戶與數據）</summary>
public sealed record ReportVerificationDto(
    string ReportNo,
    int Version,
    string CategoryCode,
    string? CategoryName,
    string? LabName,
    ReportLifecycleStatus Status,
    string? SignerName,
    DateTimeOffset? SignedAt,
    string? PdfSha256,
    bool IsSuperseded,
    bool? FileMatches);

public sealed record ReportFileResult(byte[] Content, string FileName);

/// <summary>操作結果；NotFound 表示報告不存在或無權存取（一律以 404 回應，避免洩漏存在性）</summary>
public sealed record ReportOpResult(bool Succeeded, string Message, bool NotFound = false, Guid? EntityId = null)
{
    public static ReportOpResult Ok(string message, Guid? entityId = null) => new(true, message, EntityId: entityId);

    public static ReportOpResult Fail(string message) => new(false, message);

    public static ReportOpResult Missing() => new(false, "找不到指定的報告。", NotFound: true);
}

/// <summary>可指派的使用者選項</summary>
public sealed record ReportUserOptionDto(Guid Id, string Name, string? Detail);

/// <summary>目前實驗室中可建立報告的類別</summary>
public sealed record ReportCategoryOptionDto(string Code, string Name, ConfidentialityLevel DefaultConfidentiality, bool IsActive);
