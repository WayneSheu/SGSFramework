using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Application.Contracts;

public sealed record ReleaseSearchFilter
{
    public Guid? ProjectId { get; init; }
    public IReadOnlyList<ReleaseChecklistStatus>? Status { get; init; }
}

public sealed record ReleaseSearchRequest : PagedQueryParameters<ReleaseSearchFilter>;

public sealed record ReleaseListDto(
    Guid Id, Guid ProjectId, string ProjectCode, string ProjectName, string Version, DateOnly PlannedDate,
    ReleaseChecklistStatus Status, int TotalCount, int PassCount, int BlockingOpenCount, int WarningOpenCount,
    IReadOnlyList<ReleaseSignRole> SignedRoles, DateTimeOffset CreatedAtUtc);

public sealed record ReleaseItemDto(
    string Code, string Section, string Title, string Standard, ReleaseSeverity Severity, ReleaseItemResult Result,
    string? Note, string? CheckedByName, DateTimeOffset? CheckedAt);

public sealed record ReleaseSignoffDto(ReleaseSignRole Role, string SignerName, DateTimeOffset SignedAt, string? ExceptionNote);

/// <summary>目前使用者可執行的動作（供 UI 顯示按鈕；伺服器端仍會重新驗證）。</summary>
public sealed record ReleaseActionsDto(bool CanEditItems, IReadOnlyList<ReleaseSignRole> SignableRoles);

public sealed record ReleaseDetailDto(
    Guid Id, Guid ProjectId, string ProjectCode, string ProjectName, string Version, DateOnly PlannedDate, Guid MainAuthorUserId,
    ReleaseChecklistStatus Status, string? ExceptionNote, GateStatus ReleaseGate,
    IReadOnlyList<string> BlockingOpenCodes, IReadOnlyList<string> WarningOpenCodes,
    IReadOnlyList<ReleaseItemDto> Items, IReadOnlyList<ReleaseSignoffDto> Signoffs, ReleaseActionsDto Actions, string RowVersion);

public sealed record CreateReleaseRequest(Guid ProjectId, string Version, DateOnly PlannedDate, Guid MainAuthorUserId);

public sealed record UpdateReleaseItemRequest(ReleaseItemResult Result, string? Note, string RowVersion);

public sealed record SignReleaseRequest(ReleaseSignRole Role, string? ExceptionNote, string RowVersion);
