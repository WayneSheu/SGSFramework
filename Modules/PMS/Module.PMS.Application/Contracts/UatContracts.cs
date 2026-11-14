using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Application.Contracts;

public sealed record UatSearchFilter
{
    public Guid? ProjectId { get; init; }
    public IReadOnlyList<UatStatus>? Status { get; init; }
}

public sealed record UatSearchRequest : PagedQueryParameters<UatSearchFilter>;

public sealed record UatListDto(
    Guid Id, Guid ProjectId, string ProjectCode, string ProjectName, string Version, UatStatus Status,
    DateTimeOffset CreatedAtUtc, DateTimeOffset? ResultAt, bool ResultIsProxy);

/// <summary>目前使用者可執行的動作（供 UI 顯示按鈕；伺服器端仍會重新驗證）。</summary>
public sealed record UatActionsDto(bool CanRecordResult, bool CanCancel, bool CanUpload, bool CanDeleteAttachment);

public sealed record UatAttachmentDto(
    Guid Id, string FileName, string? OriginalFileName, string? ContentType, long FileSize,
    bool MaskConfirmed, bool CanPreview, string CreatedBy, DateTimeOffset CreatedAtUtc);

public sealed record UatDetailDto(
    Guid Id, Guid ProjectId, string ProjectCode, string ProjectName, string Version, string ScopeNote, UatStatus Status,
    string ContentHash, Guid CreatedByUserId, DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ResultAt, Guid? ResultByUserId, bool ResultIsProxy, string? ProxyReason, string? Comment,
    IReadOnlyList<UatAttachmentDto> Attachments, UatActionsDto Actions, string RowVersion);

public sealed record CreateUatRequest(Guid ProjectId, string Version, string ScopeNote);

/// <param name="Result">Accepted 或 Rejected。</param>
/// <param name="ContentHash">簽核者看到的驗收內容雜湊，須與目前相符。</param>
/// <param name="ProxyReason">客戶外部簽核帳號開放前由 PM 代錄，必填原因與依據。</param>
public sealed record RecordUatResultRequest(UatStatus Result, string ContentHash, string? ProxyReason, string? Comment, string RowVersion);

public sealed record CancelUatRequest(string Reason, string RowVersion);
