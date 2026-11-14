using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Paginations;

namespace SGS.Modules.PMS.Application.Contracts;

public sealed record RiskSearchFilter
{
    public Guid? ProjectId { get; init; }
    public IReadOnlyList<RiskStatus>? Status { get; init; }
    public IReadOnlyList<RiskLevel>? Level { get; init; }

    /// <summary>僅列我負責的風險。</summary>
    public bool OwnedByMe { get; init; }
}

public sealed record RiskSearchRequest : PagedQueryParameters<RiskSearchFilter>;

/// <param name="CanEdit">目前使用者可編輯此風險（PM／Tech Lead 全部欄位；風險負責人僅狀態與因應措施）。</param>
/// <param name="CanEditAll">可編輯全部欄位（標題、等級、負責人）。</param>
/// <param name="CanVoid">可作廢（PM／Tech Lead）。</param>
public sealed record RiskDto(
    Guid Id, Guid ProjectId, string ProjectCode, string ProjectName, string Title, RiskLevel Level, RiskStatus Status,
    string? Mitigation, Guid OwnerUserId, DateTimeOffset? ClosedAt, string CreatedBy, DateTimeOffset CreatedAtUtc, DateTimeOffset? UpdatedAtUtc,
    bool CanEdit, bool CanEditAll, bool CanVoid, string RowVersion);

public sealed record CreateRiskRequest(Guid ProjectId, string Title, RiskLevel Level, string? Mitigation, Guid OwnerUserId, RiskStatus? Status = null);

/// <summary>風險負責人僅能變更 <paramref name="Status"/> 與 <paramref name="Mitigation"/>；其餘欄位帶原值或省略（伺服器忽略）。</summary>
public sealed record UpdateRiskRequest(
    string Title, RiskLevel Level, string? Mitigation, Guid OwnerUserId, RiskStatus Status, string RowVersion);
