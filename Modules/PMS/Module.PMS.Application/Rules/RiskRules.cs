using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Rules;

/// <summary>風險可編輯的範圍：全部欄位（PM／Tech Lead）、僅狀態與因應措施（風險負責人）、不可編輯。</summary>
public enum RiskEditScope
{
    None = 0,
    OwnerLimited = 1,
    Full = 2
}

/// <summary>
/// 風險管理表規則（SOP 3.1，PM 風險追蹤，Tech Lead 協助），純函式：
/// PM／Tech Lead 可建立、完整編輯與作廢；風險負責人（BE／FE）僅能更新自己負責風險的狀態與因應措施；
/// 負責人須為專案內部成員；結案時記錄結案時間，重新開啟時清除。
/// </summary>
public static class RiskRules
{
    public const int MaxTitleLength = 200;
    public const int MaxMitigationLength = 1000;

    public static RiskEditScope EditScope(Guid userId, Guid ownerUserId, IReadOnlyList<(Guid UserId, RaciRole Role)> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        if (ProjectAccessPolicy.IsPm(userId, members) || ProjectAccessPolicy.IsTechLead(userId, members)) return RiskEditScope.Full;
        if (userId == ownerUserId && ProjectAccessPolicy.Evaluate(userId, members) == ProjectAccessLevel.Internal) return RiskEditScope.OwnerLimited;
        return RiskEditScope.None;
    }

    public static bool CanVoid(Guid userId, IReadOnlyList<(Guid UserId, RaciRole Role)> members) =>
        ProjectAccessPolicy.IsPm(userId, members) || ProjectAccessPolicy.IsTechLead(userId, members);

    /// <summary>負責人須為專案內部成員（PM、Tech Lead、BE、FE），客戶 Stakeholder 不可擔任。</summary>
    public static bool IsValidOwner(Guid ownerUserId, IReadOnlyList<(Guid UserId, RaciRole Role)> members) =>
        ProjectAccessPolicy.Evaluate(ownerUserId, members) == ProjectAccessLevel.Internal;

    public static string? ValidateFields(string? title, RiskLevel level, RiskStatus status, string? mitigation)
    {
        if (string.IsNullOrWhiteSpace(title)) return "風險標題為必填。";
        if (title.Trim().Length > MaxTitleLength) return $"風險標題不得超過 {MaxTitleLength} 字。";
        if (!Enum.IsDefined(level)) return "風險等級不正確。";
        if (!Enum.IsDefined(status)) return "風險狀態不正確。";
        if (mitigation is { Length: > MaxMitigationLength }) return $"因應措施不得超過 {MaxMitigationLength} 字。";
        return null;
    }

    /// <summary>狀態變更後的結案時間：改為 Closed 時記錄（已是 Closed 則保留原時間）；離開 Closed 時清除。</summary>
    public static DateTimeOffset? NextClosedAt(RiskStatus previous, RiskStatus next, DateTimeOffset? currentClosedAt, DateTimeOffset now) =>
        next != RiskStatus.Closed ? null : previous == RiskStatus.Closed ? currentClosedAt ?? now : now;
}
