using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Identities;

namespace SGSFramework.Identity.Services;

/// <summary>活動摘要（含進度）</summary>
public sealed record AccessReviewCampaignSummary(
    AccessReviewCampaign Campaign, int Total, int Kept, int Revoked, int Pending);

/// <summary>
/// 定期權限覆核 (Access Certification)。
/// - 建立活動時快照角色成員；覆核者逐筆決定「保留」或「回收」，回收立即解除角色指派。
/// - 覆核範圍：系統管理員可覆核全部；其他覆核者僅能覆核其可存取實驗室的人員（以主要實驗室判斷）。
/// - 覆核者不得覆核自己的權限（職責分離）。
/// </summary>
public interface IAccessReviewService
{
    Task<IReadOnlyList<AccessReviewCampaignSummary>> GetCampaignsAsync(CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message, Guid? CampaignId)> CreateCampaignAsync(
        string name, string? description, DateTimeOffset dueDate, IReadOnlyCollection<Guid>? roleIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccessReviewItem>> GetItemsAsync(Guid campaignId, bool onlyMyScope, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message, int Processed)> DecideAsync(
        IReadOnlyCollection<Guid> itemIds, AccessReviewDecision decision, string? comment, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message)> CloseCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);
}

public sealed class AccessReviewService(
    DbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IOrganizationIntegrationService orgIntegrationService,
    IPermissionChangeNotifier permissionChangeNotifier,
    IRoleAuditService roleAuditService,
    IRoleExpirationService roleExpirationService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AccessReviewService> logger) : IAccessReviewService
{
    public async Task<IReadOnlyList<AccessReviewCampaignSummary>> GetCampaignsAsync(CancellationToken cancellationToken = default)
    {
        var campaigns = await dbContext.Set<AccessReviewCampaign>().AsNoTracking()
            .OrderByDescending(c => c.CreatedAt).Take(100)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var ids = campaigns.Select(c => c.Id).ToList();
        var stats = await dbContext.Set<AccessReviewItem>().AsNoTracking()
            .Where(i => ids.Contains(i.CampaignId))
            .GroupBy(i => new { i.CampaignId, i.Decision })
            .Select(g => new { g.Key.CampaignId, g.Key.Decision, Count = g.Count() })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return campaigns.Select(c =>
        {
            int Count(AccessReviewDecision d) => stats.Where(s => s.CampaignId == c.Id && s.Decision == d).Sum(s => s.Count);
            int kept = Count(AccessReviewDecision.Keep), revoked = Count(AccessReviewDecision.Revoke), pending = Count(AccessReviewDecision.Pending);
            return new AccessReviewCampaignSummary(c, kept + revoked + pending, kept, revoked, pending);
        }).ToList();
    }

    public async Task<(bool Succeeded, string Message, Guid? CampaignId)> CreateCampaignAsync(
        string name, string? description, DateTimeOffset dueDate, IReadOnlyCollection<Guid>? roleIds, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return (false, "請輸入活動名稱。", null);
        if (dueDate <= DateTimeOffset.UtcNow) return (false, "覆核期限必須晚於現在。", null);

        var roleQuery = roleManager.Roles.Where(r => !r.IsDeleted);
        if (roleIds is { Count: > 0 }) roleQuery = roleQuery.Where(r => roleIds.Contains(r.Id));
        var roles = await roleQuery.ToListAsync(cancellationToken).ConfigureAwait(false);
        if (roles.Count == 0) return (false, "找不到符合範圍的角色。", null);

        var campaign = new AccessReviewCampaign
        {
            Name = name.Trim(),
            Description = description,
            DueDate = dueDate,
            RoleScope = JsonSerializer.Serialize(roleIds ?? []),
            CreatedBy = CurrentActor().Name
        };

        // 快照：建立當下的所有成員指派
        var labCache = new Dictionary<Guid, (Guid? Id, string? Name)>();
        var items = new List<AccessReviewItem>();
        foreach (var role in roles.Where(r => r.Name is not null))
        {
            var members = await userManager.GetUsersInRoleAsync(role.Name!).ConfigureAwait(false);
            foreach (var user in members.Where(u => !u.IsDeleted))
            {
                if (!labCache.TryGetValue(user.Id, out var lab))
                {
                    lab = await ResolvePrimaryLabAsync(user.Id, cancellationToken).ConfigureAwait(false);
                    labCache[user.Id] = lab;
                }

                items.Add(new AccessReviewItem
                {
                    CampaignId = campaign.Id,
                    UserId = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    FullName = user.FullName,
                    RoleId = role.Id,
                    RoleName = role.Name!,
                    PrimaryLabId = lab.Id,
                    PrimaryLabName = lab.Name
                });
            }
        }

        if (items.Count == 0) return (false, "範圍內的角色目前沒有任何成員，無需覆核。", null);

        dbContext.Set<AccessReviewCampaign>().Add(campaign);
        dbContext.Set<AccessReviewItem>().AddRange(items);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[AccessReview] 建立覆核活動 {Name}，共 {Count} 筆指派待覆核。", campaign.Name, items.Count);
        return (true, $"已建立覆核活動，共 {items.Count} 筆角色指派待覆核。", campaign.Id);
    }

    public async Task<IReadOnlyList<AccessReviewItem>> GetItemsAsync(Guid campaignId, bool onlyMyScope, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Set<AccessReviewItem>().AsNoTracking().Where(i => i.CampaignId == campaignId);

        if (onlyMyScope)
        {
            var scope = await GetReviewerLabScopeAsync(cancellationToken).ConfigureAwait(false);
            if (scope is not null)
            {
                query = query.Where(i => i.PrimaryLabId.HasValue && scope.Contains(i.PrimaryLabId.Value));
            }
        }

        return await query.OrderBy(i => i.PrimaryLabName).ThenBy(i => i.UserName).ThenBy(i => i.RoleName)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(bool Succeeded, string Message, int Processed)> DecideAsync(
        IReadOnlyCollection<Guid> itemIds, AccessReviewDecision decision, string? comment, CancellationToken cancellationToken = default)
    {
        if (decision == AccessReviewDecision.Pending) return (false, "請選擇「保留」或「回收」。", 0);
        if (itemIds.Count == 0) return (false, "未選擇任何覆核項目。", 0);
        if (decision == AccessReviewDecision.Revoke && string.IsNullOrWhiteSpace(comment))
        {
            return (false, "回收權限時請填寫原因。", 0);
        }

        var items = await dbContext.Set<AccessReviewItem>().Where(i => itemIds.Contains(i.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (items.Count == 0) return (false, "找不到指定的覆核項目。", 0);

        var campaignIds = items.Select(i => i.CampaignId).Distinct().ToList();
        bool hasClosed = await dbContext.Set<AccessReviewCampaign>()
            .AnyAsync(c => campaignIds.Contains(c.Id) && c.Status == AccessReviewCampaignStatus.Closed, cancellationToken).ConfigureAwait(false);
        if (hasClosed) return (false, "覆核活動已結束，無法再變更決定。", 0);

        var (actorId, actorName) = CurrentActor();
        if (actorId.HasValue && items.Any(i => i.UserId == actorId.Value))
        {
            return (false, "職責分離：覆核者不得覆核自己的權限，請由其他覆核者處理。", 0);
        }

        // 範圍檢查：非系統管理員僅能覆核所屬實驗室人員
        var scope = await GetReviewerLabScopeAsync(cancellationToken).ConfigureAwait(false);
        if (scope is not null && items.Any(i => !i.PrimaryLabId.HasValue || !scope.Contains(i.PrimaryLabId.Value)))
        {
            return (false, "部分項目不在您負責的實驗室範圍內，無法覆核。", 0);
        }

        int processed = 0;
        foreach (var item in items)
        {
            if (decision == AccessReviewDecision.Revoke && item.Decision != AccessReviewDecision.Revoke)
            {
                var user = await userManager.FindByIdAsync(item.UserId.ToString()).ConfigureAwait(false);
                if (user is not null && await userManager.IsInRoleAsync(user, item.RoleName).ConfigureAwait(false))
                {
                    var result = await userManager.RemoveFromRoleAsync(user, item.RoleName).ConfigureAwait(false);
                    if (!result.Succeeded)
                    {
                        logger.LogWarning("[AccessReview] 回收 {User} 的角色 {Role} 失敗：{Errors}", item.UserName, item.RoleName,
                            string.Join("; ", result.Errors.Select(e => e.Description)));
                        continue;
                    }

                    await roleExpirationService.ClearAsync(item.RoleId, [item.UserId], cancellationToken).ConfigureAwait(false);
                    await permissionChangeNotifier.NotifyUsersAsync([item.UserId.ToString()], "access-review-revoked", cancellationToken).ConfigureAwait(false);
                }
            }

            item.Decision = decision;
            item.Comment = comment;
            item.DecidedBy = actorId;
            item.DecidedByName = actorName;
            item.DecidedAt = DateTimeOffset.UtcNow;
            processed++;

            await roleAuditService.RecordAsync(item.RoleId, item.RoleName, RoleAuditActions.AccessReviewed,
                decision == AccessReviewDecision.Keep
                    ? $"權限覆核：確認保留 {item.UserName} 的角色指派"
                    : $"權限覆核：回收 {item.UserName} 的角色指派（原因：{comment}）",
                1, new { item.CampaignId, ItemId = item.Id, Decision = decision.ToString() }, cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (true, decision == AccessReviewDecision.Keep ? $"已確認保留 {processed} 筆。" : $"已回收 {processed} 筆角色指派。", processed);
    }

    public async Task<(bool Succeeded, string Message)> CloseCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var campaign = await dbContext.Set<AccessReviewCampaign>().FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken).ConfigureAwait(false);
        if (campaign is null) return (false, "找不到指定的覆核活動。");
        if (campaign.Status == AccessReviewCampaignStatus.Closed) return (false, "活動已結束。");

        int pending = await dbContext.Set<AccessReviewItem>()
            .CountAsync(i => i.CampaignId == campaignId && i.Decision == AccessReviewDecision.Pending, cancellationToken).ConfigureAwait(false);

        campaign.Status = AccessReviewCampaignStatus.Closed;
        campaign.ClosedAt = DateTimeOffset.UtcNow;
        campaign.ClosedBy = CurrentActor().Name;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (true, pending == 0 ? "覆核活動已完成並結束。" : $"覆核活動已結束，仍有 {pending} 筆未覆核（已保留於報表供追蹤）。");
    }

    /// <summary>
    /// 覆核者可覆核的實驗室範圍；回傳 null 表示不受限（系統管理員）
    /// </summary>
    private async Task<HashSet<Guid>?> GetReviewerLabScopeAsync(CancellationToken cancellationToken)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal is null) return [];

        bool isAdmin = principal.HasClaim(c =>
            (c.Type is "is_admin" or "IsSuperAdmin") && string.Equals(c.Value, "true", StringComparison.OrdinalIgnoreCase));
        if (isAdmin) return null;

        var (actorId, _) = CurrentActor();
        if (!actorId.HasValue) return [];

        var labs = await orgIntegrationService.GetUserAccessibleOrganizationsAsync(actorId.Value.ToString(), cancellationToken).ConfigureAwait(false);
        return labs.Select(l => l.TenantLabId).ToHashSet();
    }

    private async Task<(Guid? Id, string? Name)> ResolvePrimaryLabAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var orgs = await orgIntegrationService.GetUserAccessibleOrganizationsAsync(userId.ToString(), cancellationToken).ConfigureAwait(false);
            var primary = orgs?.FirstOrDefault(o => o.IsPrimary) ?? orgs?.FirstOrDefault();
            return primary is null ? (null, null) : (primary.TenantLabId, primary.Name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[AccessReview] 取得使用者 {UserId} 的主要實驗室失敗。", userId);
            return (null, null);
        }
    }

    private (Guid? Id, string Name) CurrentActor()
    {
        var user = httpContextAccessor.HttpContext?.User;
        var raw = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value;
        return (Guid.TryParse(raw, out var id) ? id : null,
                user?.Identity?.Name ?? user?.FindFirst(ClaimTypes.Name)?.Value ?? "未知使用者");
    }
}
