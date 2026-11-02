using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Entities.ReportCategories;

namespace SGSFramework.Identity.Services;

/// <summary>活動摘要（含進度）</summary>
public sealed record AccessReviewCampaignSummary(
    AccessReviewCampaign Campaign, int Total, int Kept, int Revoked, int Pending);

/// <summary>排程建立覆核活動的結果；Created = false 表示同名活動已存在或範圍內無成員（冪等，不視為失敗）</summary>
public sealed record ScheduledAccessReviewResult(
    bool Succeeded, string Message, Guid? CampaignId, bool Created, int ClosedOverdue);

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

    /// <summary>
    /// 排程（系統帳號）建立定期覆核活動：以活動名稱做冪等判斷（同一期間重試不重複建立），
    /// 可選擇先結束已逾期的覆核活動。
    /// </summary>
    Task<ScheduledAccessReviewResult> CreateScheduledCampaignAsync(
        string name, string? description, int dueInDays, IReadOnlyCollection<Guid>? roleIds, bool closeOverdue,
        CancellationToken cancellationToken = default);
}

public sealed class AccessReviewService(
    DbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IOrganizationIntegrationService orgIntegrationService,
    IPermissionChangeNotifier permissionChangeNotifier,
    IRoleAuditService roleAuditService,
    IRoleExpirationService roleExpirationService,
    IReportCategoryManagementService reportCategoryManagementService,
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

        // 全面覆核（未限定角色）時一併快照使用者直接的報告類別授權與簽署人資格；角色授權已隨角色指派覆核
        if (roleIds is not { Count: > 0 })
        {
            items.AddRange(await SnapshotReportAccessItemsAsync(campaign.Id, labCache, cancellationToken).ConfigureAwait(false));
        }

        if (items.Count == 0) return (false, "範圍內的角色目前沒有任何成員，無需覆核。", null);

        dbContext.Set<AccessReviewCampaign>().Add(campaign);
        dbContext.Set<AccessReviewItem>().AddRange(items);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        int reportItemCount = items.Count(i => i.ItemType != AccessReviewItemType.RoleMembership);
        logger.LogInformation("[AccessReview] 建立覆核活動 {Name}，共 {Count} 筆指派待覆核（含報告授權 / 簽署資格 {ReportCount} 筆）。",
            campaign.Name, items.Count, reportItemCount);
        return (true, reportItemCount == 0
            ? $"已建立覆核活動，共 {items.Count} 筆角色指派待覆核。"
            : $"已建立覆核活動，共 {items.Count} 筆待覆核（角色指派 {items.Count - reportItemCount} 筆、報告授權與簽署資格 {reportItemCount} 筆）。", campaign.Id);
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
            if (item.ItemType != AccessReviewItemType.RoleMembership)
            {
                if (decision == AccessReviewDecision.Revoke && item.Decision != AccessReviewDecision.Revoke &&
                    !await RevokeReportAccessItemAsync(item, comment!, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                item.Decision = decision;
                item.Comment = comment;
                item.DecidedBy = actorId;
                item.DecidedByName = actorName;
                item.DecidedAt = DateTimeOffset.UtcNow;
                processed++;
                continue;
            }

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
        return (true, decision == AccessReviewDecision.Keep ? $"已確認保留 {processed} 筆。" : $"已回收 {processed} 筆權限。", processed);
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

    public async Task<ScheduledAccessReviewResult> CreateScheduledCampaignAsync(
        string name, string? description, int dueInDays, IReadOnlyCollection<Guid>? roleIds, bool closeOverdue,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) return new(false, "請輸入活動名稱。", null, false, 0);
        if (dueInDays is < 1 or > 365) return new(false, "覆核天數須介於 1 到 365 天。", null, false, 0);

        int closedOverdue = 0;
        if (closeOverdue)
        {
            var now = DateTimeOffset.UtcNow;
            var overdueIds = await dbContext.Set<AccessReviewCampaign>().AsNoTracking()
                .Where(c => c.Status == AccessReviewCampaignStatus.Open && c.DueDate < now)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var id in overdueIds)
            {
                var (closed, message) = await CloseCampaignAsync(id, cancellationToken).ConfigureAwait(false);
                if (closed)
                {
                    closedOverdue++;
                    logger.LogInformation("[AccessReview] 排程結束逾期覆核活動 {CampaignId}：{Message}", id, message);
                }
            }
        }

        // 冪等：同一期間的活動名稱相同，排程重試或多節點重複觸發時不重複建立
        var trimmedName = name.Trim();
        var existingId = await dbContext.Set<AccessReviewCampaign>().AsNoTracking()
            .Where(c => c.Name == trimmedName)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (existingId.HasValue)
        {
            return new(true, $"覆核活動「{trimmedName}」已存在，略過建立。", existingId, false, closedOverdue);
        }

        var (ok, createMessage, campaignId) = await CreateCampaignAsync(
            trimmedName, description, DateTimeOffset.UtcNow.AddDays(dueInDays), roleIds, cancellationToken).ConfigureAwait(false);

        // 範圍內沒有任何成員屬正常情況（無需覆核），不視為排程失敗
        if (!ok && campaignId is null && createMessage.Contains("沒有任何成員", StringComparison.Ordinal))
        {
            return new(true, createMessage, null, false, closedOverdue);
        }

        return new(ok, createMessage, campaignId, ok, closedOverdue);
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

    /// <summary>
    /// 快照有效中的使用者直接報告類別授權與簽署人資格（RoleId 為 Guid.Empty，RoleName 為顯示描述）
    /// </summary>
    private async Task<List<AccessReviewItem>> SnapshotReportAccessItemsAsync(
        Guid campaignId, Dictionary<Guid, (Guid? Id, string? Name)> labCache, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var grants = await dbContext.Set<ReportCategoryGrant>().AsNoTracking()
            .Where(g => g.PrincipalType == GrantPrincipalType.User && g.RevokedAt == null && (g.EffectiveTo == null || g.EffectiveTo >= now))
            .Select(g => new { g.Id, UserId = g.PrincipalId, g.CategoryCode, g.Actions, g.DataScope, g.MaxConfidentiality })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var qualifications = await dbContext.Set<SignerQualification>().AsNoTracking()
            .Where(q => q.RevokedAt == null && q.ValidTo >= now)
            .Select(q => new { q.Id, q.UserId, q.CategoryCode, q.CertificateNo, q.ValidTo })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var userIds = grants.Select(g => g.UserId).Concat(qualifications.Select(q => q.UserId)).Distinct().ToList();
        var users = await dbContext.Set<ApplicationUser>().AsNoTracking()
            .Where(u => userIds.Contains(u.Id) && !u.IsDeleted)
            .Select(u => new { u.Id, u.UserName, u.FullName })
            .ToDictionaryAsync(u => u.Id, cancellationToken).ConfigureAwait(false);

        var items = new List<AccessReviewItem>();
        async Task AddAsync(Guid userId, AccessReviewItemType type, Guid subjectId, string label)
        {
            if (!users.TryGetValue(userId, out var user)) return;
            if (!labCache.TryGetValue(userId, out var lab))
            {
                lab = await ResolvePrimaryLabAsync(userId, cancellationToken).ConfigureAwait(false);
                labCache[userId] = lab;
            }

            items.Add(new AccessReviewItem
            {
                CampaignId = campaignId,
                UserId = userId,
                UserName = user.UserName ?? string.Empty,
                FullName = user.FullName,
                RoleId = Guid.Empty,
                RoleName = label.Length > 256 ? label[..256] : label,
                PrimaryLabId = lab.Id,
                PrimaryLabName = lab.Name,
                ItemType = type,
                SubjectId = subjectId
            });
        }

        foreach (var g in grants)
        {
            await AddAsync(g.UserId, AccessReviewItemType.ReportCategoryGrant, g.Id,
                $"報告授權：{g.CategoryCode}／{g.Actions}（{g.DataScope}，{g.MaxConfidentiality}）").ConfigureAwait(false);
        }

        foreach (var q in qualifications)
        {
            await AddAsync(q.UserId, AccessReviewItemType.SignerQualification, q.Id,
                $"簽署資格：{q.CategoryCode}（證號 {q.CertificateNo}，至 {q.ValidTo:yyyy-MM-dd}）").ConfigureAwait(false);
        }

        return items;
    }

    /// <summary>回收報告授權或簽署資格（交由報告類別權限管理服務撤銷並通知）</summary>
    private async Task<bool> RevokeReportAccessItemAsync(AccessReviewItem item, string comment, CancellationToken cancellationToken)
    {
        if (!item.SubjectId.HasValue) return false;

        var reason = $"權限覆核回收：{comment}";
        var (ok, message) = item.ItemType == AccessReviewItemType.SignerQualification
            ? await reportCategoryManagementService.RevokeQualificationAsync(item.SubjectId.Value, reason, cancellationToken).ConfigureAwait(false)
            : await reportCategoryManagementService.RevokeGrantAsync(item.SubjectId.Value, reason, cancellationToken).ConfigureAwait(false);

        // 已於他處撤銷者視為已回收
        if (!ok && !message.Contains("已撤銷", StringComparison.Ordinal))
        {
            logger.LogWarning("[AccessReview] 回收 {User} 的 {Item} 失敗：{Message}", item.UserName, item.RoleName, message);
            return false;
        }

        return true;
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
