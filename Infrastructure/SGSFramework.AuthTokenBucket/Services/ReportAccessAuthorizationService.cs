using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Entities.ReportCategories;
using SGSFramework.Core.Abstractions.ReportAccess;
using ZiggyCreatures.Caching.Fusion;

namespace SGSFramework.AuthTokenBucket.Services;

/// <summary>報告授權快照快取標籤：類別主檔或承作實驗室異動影響全體使用者時批次清除</summary>
public static class ReportAccessCacheTags
{
    public const string All = "rpt-access:all";
}

/// <summary>
/// 報告資料層授權：彙整使用者於區域實驗室下的歸屬、類別授權（使用者 + 有效角色）、承作類別與簽署資格，
/// 交由 <see cref="ReportAccessPolicy"/> 判定。快照以「使用者 × 實驗室」快取，授權異動時由
/// <see cref="IPermissionChangeNotifier"/> 依使用者標籤清除；快取期限不超過最近一筆授權或資格的生效 / 失效時點。
/// </summary>
public sealed class ReportAccessAuthorizationService(
    DbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IUserLabRepository userLabRepository,
    IRoleEffectivePermissionProvider roleEffectivePermissionProvider,
    IFusionCache cache,
    TimeProvider timeProvider,
    ILogger<ReportAccessAuthorizationService> logger) : IReportAccessAuthorizationService
{
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan MinCacheExpiration = TimeSpan.FromSeconds(1);

    public async Task<ReportAccessDecision> AuthorizeAsync(
        ClaimsPrincipal user,
        ReportAccessContext context,
        ReportActions action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(context);

        var subject = await GetSubjectAsync(user, context.TenantLabId, cancellationToken).ConfigureAwait(false);
        if (subject is null)
        {
            return ReportAccessDecision.Deny("無法識別使用者身分。");
        }

        var decision = ReportAccessPolicy.Evaluate(subject, context, action);
        if (!decision.IsAllowed)
        {
            logger.LogInformation("[ReportAccess] 拒絕 {UserId} 對類別 {Category} / 實驗室 {LabId} 執行 {Action}：{Reason}",
                subject.UserId, context.CategoryCode, context.TenantLabId, action, decision.Reason);
        }

        return decision;
    }

    public async Task<bool> HasCategoryActionAsync(
        ClaimsPrincipal user,
        Guid tenantLabId,
        IReadOnlyCollection<string> categoryCodes,
        ReportActions action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(categoryCodes);

        var subject = await GetSubjectAsync(user, tenantLabId, cancellationToken).ConfigureAwait(false);
        return subject is not null && ReportAccessPolicy.HasCategoryAction(subject, categoryCodes, action);
    }

    public async Task<ReportAccessSubject?> GetSubjectAsync(
        ClaimsPrincipal user,
        Guid tenantLabId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (user.Identity is not { IsAuthenticated: true } || tenantLabId == Guid.Empty)
        {
            return null;
        }

        var userId = AuthorizationClaimHelper.ResolveUserId(user);
        if (userId is null)
        {
            return null;
        }

        var snapshot = await cache.GetOrSetAsync<SubjectSnapshot?>(
            $"rpt_access:{userId}:{tenantLabId}",
            async (ctx, ct) =>
            {
                var (result, nextChangeAt) = await LoadSnapshotAsync(userId.Value, tenantLabId, ct).ConfigureAwait(false);
                if (nextChangeAt.HasValue)
                {
                    var untilChange = nextChangeAt.Value - timeProvider.GetUtcNow();
                    ctx.Options.Duration = untilChange < MinCacheExpiration ? MinCacheExpiration
                        : untilChange < CacheExpiration ? untilChange : CacheExpiration;
                }
                return result;
            },
            CacheExpiration,
            tags: [PermissionCacheTags.User(userId.Value.ToString()), ReportAccessCacheTags.All],
            token: cancellationToken
        ).ConfigureAwait(false);

        if (snapshot is null)
        {
            return null;
        }

        return new ReportAccessSubject
        {
            UserId = userId.Value,
            TenantLabId = tenantLabId,
            IsSuperAdmin = AuthorizationClaimHelper.IsSuperAdmin(user),
            IsLabMember = snapshot.IsLabMember,
            LabCategoryCodes = snapshot.LabCategoryCodes,
            Grants = snapshot.Grants,
            SignerCategoryCodes = snapshot.SignerCategoryCodes
        };
    }

    private async Task<(SubjectSnapshot? Snapshot, DateTimeOffset? NextChangeAt)> LoadSnapshotAsync(
        Guid userId, Guid tenantLabId, CancellationToken cancellationToken)
    {
        var appUser = await userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (appUser is null || appUser.IsDeleted)
        {
            return (null, null);
        }

        var now = timeProvider.GetUtcNow();
        var boundaries = new List<DateTimeOffset>();

        // 1. 實驗室歸屬（UserLabMapping 有效期間內）
        var labMappings = await userLabRepository.GetAccessibleLabsAsync(userId, cancellationToken).ConfigureAwait(false);
        bool isLabMember = labMappings.Any(m => m.TenantLabId == tenantLabId && m.IsValidAt(now));

        // 2. 此實驗室承作之啟用中類別
        var activeCategoryCodes = await dbContext.Set<ReportCategory>().AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => c.Code)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var activeCategories = new HashSet<string>(activeCategoryCodes, StringComparer.OrdinalIgnoreCase);

        var labCategoryCodes = (await dbContext.Set<ReportCategoryLabAssignment>().AsNoTracking()
                .Where(a => a.TenantLabId == tenantLabId)
                .Select(a => a.CategoryCode)
                .ToListAsync(cancellationToken).ConfigureAwait(false))
            .Where(activeCategories.Contains)
            .ToList();

        // 3. 類別授權：使用者直接授權 + 於此實驗室有效之角色授權（未撤銷、尚未過期，含未來生效者以計算快取期限）
        var roleIds = await roleEffectivePermissionProvider.GetEffectiveRoleIdsAsync(appUser, tenantLabId, cancellationToken).ConfigureAwait(false);

        var grantRows = await dbContext.Set<ReportCategoryGrant>().AsNoTracking()
            .Where(g => g.RevokedAt == null &&
                        (g.EffectiveTo == null || g.EffectiveTo >= now) &&
                        (g.TenantLabId == null || g.TenantLabId == tenantLabId) &&
                        ((g.PrincipalType == GrantPrincipalType.User && g.PrincipalId == userId) ||
                         (g.PrincipalType == GrantPrincipalType.Role && roleIds.Contains(g.PrincipalId))))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var grants = new List<EffectiveReportGrant>();
        foreach (var g in grantRows.Where(g => activeCategories.Contains(g.CategoryCode)))
        {
            if (g.EffectiveFrom > now)
            {
                boundaries.Add(g.EffectiveFrom);
                continue;
            }

            if (g.EffectiveTo.HasValue) boundaries.Add(g.EffectiveTo.Value);
            grants.Add(new EffectiveReportGrant(g.CategoryCode, g.TenantLabId, g.Actions, g.DataScope, g.MaxConfidentiality));
        }

        // 4. 簽署資格
        var qualifications = await dbContext.Set<SignerQualification>().AsNoTracking()
            .Where(q => q.UserId == userId && q.TenantLabId == tenantLabId && q.RevokedAt == null && q.ValidTo >= now)
            .Select(q => new { q.CategoryCode, q.ValidFrom, q.ValidTo })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var signerCategoryCodes = new List<string>();
        foreach (var q in qualifications)
        {
            if (q.ValidFrom > now)
            {
                boundaries.Add(q.ValidFrom);
                continue;
            }

            boundaries.Add(q.ValidTo);
            signerCategoryCodes.Add(q.CategoryCode);
        }

        var snapshot = new SubjectSnapshot(
            isLabMember,
            labCategoryCodes,
            grants,
            signerCategoryCodes.Distinct(StringComparer.OrdinalIgnoreCase).ToList());

        return (snapshot, boundaries.Count > 0 ? boundaries.Min() : null);
    }

    /// <summary>可快取之授權快照（不含來自 Token 的管理員宣告）</summary>
    private sealed record SubjectSnapshot(
        bool IsLabMember,
        List<string> LabCategoryCodes,
        List<EffectiveReportGrant> Grants,
        List<string> SignerCategoryCodes);
}
