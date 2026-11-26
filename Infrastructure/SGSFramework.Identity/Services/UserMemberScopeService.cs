using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Identities;
using SGSFramework.Core.Rules;

namespace SGSFramework.Identity.Services;

/// <summary>
/// 使用者維護範圍實作：由 RUL 的 Row 規則決定。沒有適用於其角色的 Row 規則（或為系統管理員、或引擎未安裝）時不限制；
/// 有規則時只回傳規則允許的「成員」（目前定義為共同歸屬實驗室的使用者，含自己）。
/// </summary>
public sealed class UserMemberScopeService(
    IServiceProvider services,
    ICurrentUserService currentUser,
    IOrganizationIntegrationService org,
    UserManager<ApplicationUser> userManager) : IUserMemberScope
{
    public async Task<IQueryable<T>> ApplyAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default) where T : ApplicationUser
    {
        var engine = services.GetService(typeof(IRuleEngine)) as IRuleEngine;
        if (engine is null || currentUser.IsAdmin || !currentUser.IsAuthenticated) return query;

        var probe = new RuleUserContext { UserId = currentUser.UserId ?? string.Empty, Roles = currentUser.Roles };
        if (!engine.HasRowRules<T>(probe)) return query;

        var members = await org.GetMemberUserIdsAsync(probe.UserId, cancellationToken).ConfigureAwait(false);
        var ctx = new RuleUserContext { UserId = probe.UserId, Roles = probe.Roles, MemberUserIds = members };
        return engine.ApplyRowSecurity(query, ctx);
    }

    public async Task<bool> CanAccessUserAsync(Guid targetUserId, CancellationToken cancellationToken = default)
    {
        var scoped = await ApplyAsync(userManager.Users, cancellationToken).ConfigureAwait(false);
        return await scoped.AnyAsync(u => u.Id == targetUserId, cancellationToken).ConfigureAwait(false);
    }
}
