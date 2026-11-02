using SGSFramework.Core.Abstractions.Entities.ReportCategories;
using SGSFramework.Core.Abstractions.Permissions.Entities;

namespace SGSFramework.AuthTokenBucket.Services;

public sealed record ReportCategoryDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    ConfidentialityLevel DefaultConfidentiality,
    string? RegulationTags,
    int DisplayOrder,
    bool IsActive,
    IReadOnlyList<Guid> TenantLabIds,
    string RowVersion);

/// <summary>新增（Id 為 null）或更新報告類別；類別代碼建立後不可變更</summary>
public sealed record SaveReportCategoryRequest(
    Guid? Id,
    string Code,
    string Name,
    string? Description,
    ConfidentialityLevel DefaultConfidentiality,
    string? RegulationTags,
    int DisplayOrder,
    bool IsActive,
    string? RowVersion);

public sealed record SetCategoryLabsRequest(List<Guid> TenantLabIds);

public sealed record ReportLabOptionDto(Guid TenantLabId, string Code, string Name);

public sealed record ReportPrincipalOptionDto(GrantPrincipalType Type, Guid Id, string Name, string? Detail);

public sealed record ReportCategoryGrantDto(
    Guid Id,
    GrantPrincipalType PrincipalType,
    Guid PrincipalId,
    string PrincipalName,
    string CategoryCode,
    Guid? TenantLabId,
    string? LabName,
    ReportActions Actions,
    ReportDataScope DataScope,
    ConfidentialityLevel MaxConfidentiality,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? Reason,
    string Status,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? RevokedAt,
    string? RevokedByName,
    string? RevokeReason);

public sealed record CreateReportCategoryGrantRequest(
    GrantPrincipalType PrincipalType,
    Guid PrincipalId,
    string CategoryCode,
    Guid? TenantLabId,
    ReportActions Actions,
    ReportDataScope DataScope,
    ConfidentialityLevel MaxConfidentiality,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? Reason);

public sealed record SignerQualificationDto(
    Guid Id,
    Guid UserId,
    string UserName,
    string FullName,
    string CategoryCode,
    Guid TenantLabId,
    string LabName,
    string CertificateNo,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidTo,
    string? Remark,
    string Status,
    string CreatedBy,
    DateTimeOffset? RevokedAt,
    string? RevokedByName,
    string? RevokeReason);

public sealed record CreateSignerQualificationRequest(
    Guid UserId,
    string CategoryCode,
    Guid TenantLabId,
    string CertificateNo,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidTo,
    string? Remark,
    string? Reason);

public sealed record ReportGrantChangeRequestDto(
    Guid Id,
    ReportGrantChangeType RequestType,
    string Summary,
    Guid? SubjectUserId,
    string? RequestReason,
    RoleChangeRequestStatus Status,
    string RequestedByName,
    DateTimeOffset RequestedAt,
    string? ReviewedByName,
    DateTimeOffset? ReviewedAt,
    string? ReviewComment,
    bool CanApprove,
    bool IsMine);

/// <summary>授權送出結果：敏感授權轉為待審核（PendingApproval），其餘立即生效</summary>
public sealed record ReportGrantSubmitResult(bool Succeeded, string Message, bool PendingApproval = false, Guid? EntityId = null)
{
    public static ReportGrantSubmitResult Fail(string message) => new(false, message);
}
