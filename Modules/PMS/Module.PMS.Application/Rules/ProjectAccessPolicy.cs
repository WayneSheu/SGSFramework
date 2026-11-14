using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Rules;

/// <summary>資料列授權結果：None＝非成員（呼叫端一律以 404 呈現，防 IDOR）。</summary>
public enum ProjectAccessLevel
{
    None = 0,
    /// <summary>外部 Stakeholder：僅可見「客戶可見」投影。</summary>
    Stakeholder = 1,
    /// <summary>內部成員（PM／Tech Lead／BE／FE）。</summary>
    Internal = 2
}

/// <summary>
/// 專案資料列授權（SOP 8、SEC-05）：僅專案成員可存取；僅有 Stakeholder 角色者僅得見客戶可見資料。
/// 純函式，仿 ReportAccessPolicy。
/// </summary>
public static class ProjectAccessPolicy
{
    public static ProjectAccessLevel Evaluate(Guid userId, IEnumerable<(Guid UserId, RaciRole Role)> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        var roles = members.Where(m => m.UserId == userId).Select(m => m.Role).ToList();
        if (roles.Count == 0) return ProjectAccessLevel.None;
        return roles.Any(r => r != RaciRole.Stakeholder) ? ProjectAccessLevel.Internal : ProjectAccessLevel.Stakeholder;
    }

    /// <summary>是否為專案 Tech Lead（放行 Gate 之 A）。</summary>
    public static bool IsTechLead(Guid userId, IEnumerable<(Guid UserId, RaciRole Role)> members) =>
        members.Any(m => m.UserId == userId && m.Role == RaciRole.TechLead);

    /// <summary>是否為專案 PM。</summary>
    public static bool IsPm(Guid userId, IEnumerable<(Guid UserId, RaciRole Role)> members) =>
        members.Any(m => m.UserId == userId && m.Role == RaciRole.Pm);
}
