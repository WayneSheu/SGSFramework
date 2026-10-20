using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.RolePermissions;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using SGSFramework.Core.Extensions;

namespace SGSFramework.AuthTokenBucket.Services;

/// <summary>SoD 規則違反項目</summary>
public sealed record SodViolation(Guid RuleId, string RuleName, string PermissionKeyA, string PermissionKeyB);

/// <summary>申請清單檢視：申請內容 + 目前使用者可否核准 / 是否為提出者</summary>
public sealed record RoleChangeRequestView(RoleChangeRequest Request, bool CanApprove, bool IsMine);

/// <summary>送出變更申請的結果</summary>
public sealed record RoleChangeSubmitResult(bool Succeeded, string Message, Guid? RequestId = null, bool AppliedImmediately = false,
    IReadOnlyList<SodViolation>? Violations = null);

/// <summary>
/// 角色權限治理：雙重審批 (Maker-Checker) 與職能分立 (SoD)。
/// - 角色權限異動一律先建立申請，須由「非提出者」核准後才套用。
/// - 送出與核准時皆檢查 SoD 規則，違反時阻擋。
/// - 組態 RoleGovernance:RequireApproval = false 時改為直接套用（仍檢查 SoD），供單人維運環境使用。
/// </summary>
public interface IRoleGovernanceService
{
    Task<RoleChangeSubmitResult> SubmitRolePermissionChangeAsync(UpdateRoleGlobalPermissionsRequest request, string? reason, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoleChangeRequest>> GetChangeRequestsAsync(RoleChangeRequestStatus? status, int take = 200, CancellationToken cancellationToken = default);

    /// <summary>含目前使用者是否可核准等資訊的申請清單（供審核畫面使用）</summary>
    Task<IReadOnlyList<RoleChangeRequestView>> GetChangeRequestViewsAsync(RoleChangeRequestStatus? status, CancellationToken cancellationToken = default);

    Task<RoleChangeSubmitResult> ApproveAsync(Guid requestId, string? comment, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message)> RejectAsync(Guid requestId, string? comment, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message)> CancelAsync(Guid requestId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SodViolation>> CheckSodAsync(IEnumerable<string> permissionKeys, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SodRule>> GetSodRulesAsync(CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message, SodRule? Rule)> SaveSodRuleAsync(SodRule rule, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message)> DeleteSodRuleAsync(Guid ruleId, CancellationToken cancellationToken = default);

    /// <summary>是否啟用雙重審批（組態 RoleGovernance:RequireApproval，預設 true）</summary>
    bool RequireApproval { get; }

    /// <summary>提出者可自行核准的例外：持有啟用中的系統內建角色，且最高管理階層大於目標角色</summary>
    Task<bool> CanSelfApproveAsync(Guid actorId, Guid targetRoleId, CancellationToken cancellationToken = default);
}

public sealed class RoleGovernanceService(
    DbContext dbContext,
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IPermissionManagementService permissionService,
    IPermissionRegistry permissionRegistry,
    IRoleAuditService roleAuditService,
    IPermissionChangeNotifier permissionChangeNotifier,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    ILogger<RoleGovernanceService> logger) : IRoleGovernanceService
{
    public bool RequireApproval => configuration.GetValue("RoleGovernance:RequireApproval", true);

    // ===== Maker：送出申請 =====

    public async Task<RoleChangeSubmitResult> SubmitRolePermissionChangeAsync(
        UpdateRoleGlobalPermissionsRequest request, string? reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Guid.TryParse(request.RoleId, out var roleId)) return new(false, "角色識別碼格式無效。");

        var role = await roleManager.FindByIdAsync(request.RoleId).ConfigureAwait(false);
        if (role?.Name is null || role.IsDeleted) return new(false, "找不到指定的角色。");
        if (role.Status is not (RoleStatus.Active or RoleStatus.PendingApproval))
        {
            return new(false, $"角色 [{role.Name}] 目前為「{role.Status.GetDisplayName()}」，無法異動權限設定。");
        }

        var target = Normalize(request.PermissionKeys);

        // 職能分立檢查：異動後的權限組合不得違反互斥規則
        var violations = await CheckSodAsync(target, cancellationToken).ConfigureAwait(false);
        if (violations.Count > 0)
        {
            return new(false, $"違反職能分立 (SoD) 規則：{string.Join("；", violations.Select(v => v.RuleName))}", Violations: violations);
        }

        var current = Normalize((await permissionService.GetRoleGlobalPermissionsAsync(request.RoleId, cancellationToken).ConfigureAwait(false))?.GrantedPermissionKeys);
        var added = target.Except(current, StringComparer.OrdinalIgnoreCase).OrderBy(k => k).ToList();
        var removed = current.Except(target, StringComparer.OrdinalIgnoreCase).OrderBy(k => k).ToList();
        if (added.Count == 0 && removed.Count == 0) return new(false, "權限設定沒有任何變更。");

        if (!RequireApproval)
        {
            var (ok, msg) = await permissionService.UpdateRolePermissionsAsync(request with { PermissionKeys = target }, cancellationToken).ConfigureAwait(false);
            return new(ok, ok ? "權限已更新（未啟用雙重審批）。" : msg, AppliedImmediately: ok);
        }

        bool hasPending = await dbContext.Set<RoleChangeRequest>()
            .AnyAsync(x => x.RoleId == roleId && x.Status == RoleChangeRequestStatus.Pending, cancellationToken).ConfigureAwait(false);
        if (hasPending) return new(false, $"角色 [{role.Name}] 已有待審核的權限變更申請，請待審核完成或撤回後再送出。");

        var (actorId, actorName) = CurrentActor();
        var memberCount = (await userManager.GetUsersInRoleAsync(role.Name).ConfigureAwait(false)).Count;

        var entity = new RoleChangeRequest
        {
            RoleId = roleId,
            RoleName = role.Name,
            TargetPermissionKeys = JsonSerializer.Serialize(target),
            AddedPermissionKeys = JsonSerializer.Serialize(added),
            RemovedPermissionKeys = JsonSerializer.Serialize(removed),
            AffectedUserCount = memberCount,
            RequestReason = reason,
            RequestedBy = actorId ?? Guid.Empty,
            RequestedByName = actorName,
            PreviousRoleStatus = role.Status
        };
        dbContext.Set<RoleChangeRequest>().Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 送審期間角色標示為「待審核」（原為啟用中者仍維持既有權限，直到審核結束）
        if (role.Status == RoleStatus.Active)
        {
            role.Status = RoleStatus.PendingApproval;
            await roleManager.UpdateAsync(role).ConfigureAwait(false);
        }

        await roleAuditService.RecordAsync(roleId, role.Name, RoleAuditActions.ChangeRequested,
            $"提出權限變更申請：新增 {added.Count} 項、移除 {removed.Count} 項，影響 {memberCount} 位成員" +
            (string.IsNullOrWhiteSpace(reason) ? string.Empty : $"（原因：{reason}）"),
            memberCount, new { RequestId = entity.Id, Added = added, Removed = removed }, cancellationToken).ConfigureAwait(false);

        return new(true, "已送出權限變更申請，待其他審核者核准後生效。", entity.Id);
    }

    public async Task<IReadOnlyList<RoleChangeRequest>> GetChangeRequestsAsync(
        RoleChangeRequestStatus? status, int take = 200, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Set<RoleChangeRequest>().AsNoTracking();
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);

        return await query.OrderByDescending(x => x.RequestedAt).Take(Math.Clamp(take, 1, 1000))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    // ===== Checker：核准 / 駁回 =====

    public async Task<RoleChangeSubmitResult> ApproveAsync(Guid requestId, string? comment, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Set<RoleChangeRequest>().FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken).ConfigureAwait(false);
        if (entity is null) return new(false, "找不到指定的申請。");
        if (entity.Status != RoleChangeRequestStatus.Pending) return new(false, "此申請已處理，無法再次審核。");

        var (actorId, actorName) = CurrentActor();
        bool isSelfApproval = actorId.HasValue && actorId.Value == entity.RequestedBy;
        if (isSelfApproval && !await CanSelfApproveAsync(actorId!.Value, entity.RoleId, cancellationToken).ConfigureAwait(false))
        {
            return new(false, "雙重審批規定：提出者不得核准自己的申請，請由其他審核者處理。" +
                              "（持有系統內建角色且管理階層高於申請角色者除外）");
        }

        var role = await roleManager.FindByIdAsync(entity.RoleId.ToString()).ConfigureAwait(false);
        if (role?.Name is null || role.IsDeleted) return new(false, "角色已不存在，請駁回此申請。");
        if (role.Status is not (RoleStatus.Active or RoleStatus.PendingApproval))
        {
            return new(false, $"角色目前為「{role.Status.GetDisplayName()}」，無法套用權限變更，請駁回此申請。");
        }

        var target = JsonSerializer.Deserialize<List<string>>(entity.TargetPermissionKeys) ?? [];

        // 核准時再次檢查：規則可能在送出後新增
        var violations = await CheckSodAsync(target, cancellationToken).ConfigureAwait(false);
        if (violations.Count > 0)
        {
            return new(false, $"違反職能分立 (SoD) 規則，無法核准：{string.Join("；", violations.Select(v => v.RuleName))}", Violations: violations);
        }

        var (ok, msg) = await permissionService.UpdateRolePermissionsAsync(
            new UpdateRoleGlobalPermissionsRequest { RoleId = entity.RoleId.ToString(), PermissionKeys = target },
            cancellationToken).ConfigureAwait(false);
        if (!ok) return new(false, $"套用權限變更失敗：{msg}");

        entity.Status = RoleChangeRequestStatus.Approved;
        entity.ReviewedBy = actorId;
        entity.ReviewedByName = actorName;
        entity.ReviewedAt = DateTimeOffset.UtcNow;
        entity.ReviewComment = comment;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await roleAuditService.RecordAsync(entity.RoleId, entity.RoleName, RoleAuditActions.ChangeApproved,
            (isSelfApproval ? "提出者以高階系統角色自行核准權限變更申請" : $"核准 {entity.RequestedByName} 提出的權限變更申請") + (string.IsNullOrWhiteSpace(comment) ? string.Empty : $"（意見：{comment}）"),
            entity.AffectedUserCount, new { RequestId = entity.Id }, cancellationToken).ConfigureAwait(false);

        var statusNote = await SettleRoleStatusAsync(entity, approved: true, actorId, cancellationToken).ConfigureAwait(false);
        return new(true, "已核准並套用權限變更。" + statusNote, entity.Id, AppliedImmediately: true);
    }

    public async Task<(bool Succeeded, string Message)> RejectAsync(Guid requestId, string? comment, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(comment)) return (false, "駁回時請填寫原因。");

        var entity = await dbContext.Set<RoleChangeRequest>().FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken).ConfigureAwait(false);
        if (entity is null) return (false, "找不到指定的申請。");
        if (entity.Status != RoleChangeRequestStatus.Pending) return (false, "此申請已處理，無法再次審核。");

        var (actorId, actorName) = CurrentActor();
        entity.Status = RoleChangeRequestStatus.Rejected;
        entity.ReviewedBy = actorId;
        entity.ReviewedByName = actorName;
        entity.ReviewedAt = DateTimeOffset.UtcNow;
        entity.ReviewComment = comment;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await roleAuditService.RecordAsync(entity.RoleId, entity.RoleName, RoleAuditActions.ChangeRejected,
            $"駁回 {entity.RequestedByName} 提出的權限變更申請（原因：{comment}）",
            0, new { RequestId = entity.Id }, cancellationToken).ConfigureAwait(false);

        await SettleRoleStatusAsync(entity, approved: false, actorId, cancellationToken).ConfigureAwait(false);
        return (true, "已駁回申請。");
    }

    public async Task<(bool Succeeded, string Message)> CancelAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Set<RoleChangeRequest>().FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken).ConfigureAwait(false);
        if (entity is null) return (false, "找不到指定的申請。");
        if (entity.Status != RoleChangeRequestStatus.Pending) return (false, "此申請已處理，無法撤回。");

        var (actorId, actorName) = CurrentActor();
        if (actorId != entity.RequestedBy) return (false, "僅提出者可撤回申請。");

        entity.Status = RoleChangeRequestStatus.Cancelled;
        entity.ReviewedBy = actorId;
        entity.ReviewedByName = actorName;
        entity.ReviewedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await roleAuditService.RecordAsync(entity.RoleId, entity.RoleName, RoleAuditActions.ChangeRejected,
            "提出者撤回權限變更申請", 0, new { RequestId = entity.Id }, cancellationToken).ConfigureAwait(false);
        await SettleRoleStatusAsync(entity, approved: false, actorId, cancellationToken).ConfigureAwait(false);
        return (true, "已撤回申請。");
    }

    public async Task<IReadOnlyList<RoleChangeRequestView>> GetChangeRequestViewsAsync(
        RoleChangeRequestStatus? status, CancellationToken cancellationToken = default)
    {
        var requests = await GetChangeRequestsAsync(status, cancellationToken: cancellationToken).ConfigureAwait(false);
        var (actorId, _) = CurrentActor();

        var result = new List<RoleChangeRequestView>(requests.Count);
        foreach (var r in requests)
        {
            bool canApprove = r.Status == RoleChangeRequestStatus.Pending && actorId.HasValue &&
                              (actorId.Value != r.RequestedBy ||
                               await CanSelfApproveAsync(actorId.Value, r.RoleId, cancellationToken).ConfigureAwait(false));
            result.Add(new RoleChangeRequestView(r, canApprove, actorId.HasValue && actorId.Value == r.RequestedBy));
        }

        return result;
    }

    /// <summary>
    /// 審核結束後恢復角色狀態：
    /// - 送審前為「啟用中」：核准 / 駁回 / 撤回後一律恢復啟用中
    /// - 送審前為「待審核」（新建角色）：核准時一併啟用（建立者不得自行核准，高階系統角色例外）；駁回 / 撤回則維持待審核
    /// </summary>
    /// <returns>附加於結果訊息的狀態說明</returns>
    private async Task<string> SettleRoleStatusAsync(RoleChangeRequest entity, bool approved, Guid? actorId, CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByIdAsync(entity.RoleId.ToString()).ConfigureAwait(false);
        if (role?.Name is null || role.IsDeleted || role.Status != RoleStatus.PendingApproval) return string.Empty;

        bool isNewRole = entity.PreviousRoleStatus == RoleStatus.PendingApproval;
        if (isNewRole)
        {
            if (!approved) return string.Empty;

            var creatorId = await dbContext.Set<RoleAuditLog>().AsNoTracking()
                .Where(l => l.RoleId == entity.RoleId && l.Action == RoleAuditActions.Created)
                .OrderBy(l => l.CreatedAt)
                .Select(l => l.ActorUserId)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (creatorId.HasValue && creatorId == actorId &&
                !await CanSelfApproveAsync(actorId!.Value, entity.RoleId, cancellationToken).ConfigureAwait(false))
            {
                return "角色建立者不得核准新角色，角色維持「待審核」，請由其他審核者啟用。";
            }
        }

        role.Status = RoleStatus.Active;
        var result = await roleManager.UpdateAsync(role).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            logger.LogWarning("[RoleGovernance] 恢復角色 {RoleName} 狀態失敗：{Errors}", role.Name, string.Join("; ", result.Errors.Select(e => e.Description)));
            return string.Empty;
        }

        await permissionChangeNotifier.NotifyRoleMembersAsync(role.Name, "role-status-changed", cancellationToken).ConfigureAwait(false);
        await roleAuditService.RecordAsync(entity.RoleId, role.Name, RoleAuditActions.StatusChanged,
            isNewRole ? "新角色隨權限變更核准一併啟用" : "權限變更審核結束，狀態恢復「啟用中」",
            0, new { RequestId = entity.Id, OldStatus = RoleStatus.PendingApproval.ToString(), NewStatus = RoleStatus.Active.ToString() },
            cancellationToken).ConfigureAwait(false);

        return isNewRole ? "角色已同步啟用。" : string.Empty;
    }

    /// <summary>
    /// 提出者自行核准的例外條件：持有「啟用中的系統內建角色」，且其最高管理階層大於申請角色的階層。
    /// 例如系統管理員 (Lv.100) 可直接核准 DEV (Lv.50) 的權限變更；同階或較低者仍須他人核准。
    /// </summary>
    public async Task<bool> CanSelfApproveAsync(Guid actorId, Guid targetRoleId, CancellationToken cancellationToken = default)
    {
        var actor = await userManager.FindByIdAsync(actorId.ToString()).ConfigureAwait(false);
        if (actor is null) return false;

        var roleNames = await userManager.GetRolesAsync(actor).ConfigureAwait(false);
        if (roleNames.Count == 0) return false;

        var actorRoles = await roleManager.Roles.AsNoTracking()
            .Where(r => r.Name != null && roleNames.Contains(r.Name) && !r.IsDeleted && r.Status == RoleStatus.Active)
            .Select(r => new { r.IsSystemRole, r.HierarchyLevel })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (!actorRoles.Any(r => r.IsSystemRole)) return false;

        var targetLevel = await roleManager.Roles.AsNoTracking()
            .Where(r => r.Id == targetRoleId)
            .Select(r => (int?)r.HierarchyLevel)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return targetLevel.HasValue && actorRoles.Max(r => r.HierarchyLevel) > targetLevel.Value;
    }

    // ===== SoD 職能分立 =====

    public async Task<IReadOnlyList<SodViolation>> CheckSodAsync(IEnumerable<string> permissionKeys, CancellationToken cancellationToken = default)
    {
        var keys = new HashSet<string>(permissionKeys ?? [], StringComparer.OrdinalIgnoreCase);
        if (keys.Count < 2) return [];

        var rules = await dbContext.Set<SodRule>().AsNoTracking().Where(r => r.IsEnabled)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return rules
            .Where(r => keys.Contains(r.PermissionKeyA) && keys.Contains(r.PermissionKeyB))
            .Select(r => new SodViolation(r.Id, r.Name, r.PermissionKeyA, r.PermissionKeyB))
            .ToList();
    }

    public async Task<IReadOnlyList<SodRule>> GetSodRulesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Set<SodRule>().AsNoTracking().OrderBy(r => r.Name).ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<(bool Succeeded, string Message, SodRule? Rule)> SaveSodRuleAsync(SodRule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (string.IsNullOrWhiteSpace(rule.Name)) return (false, "請輸入規則名稱。", null);
        if (string.IsNullOrWhiteSpace(rule.PermissionKeyA) || string.IsNullOrWhiteSpace(rule.PermissionKeyB))
            return (false, "請選擇兩項互斥權限。", null);
        if (string.Equals(rule.PermissionKeyA, rule.PermissionKeyB, StringComparison.OrdinalIgnoreCase))
            return (false, "互斥權限不可為同一項。", null);
        foreach (var key in new[] { rule.PermissionKeyA, rule.PermissionKeyB })
        {
            if (!permissionRegistry.TryGetPermission(key, out _)) return (false, $"權限「{key}」不存在。", null);
        }

        var set = dbContext.Set<SodRule>();
        var existing = rule.Id == Guid.Empty ? null : await set.FirstOrDefaultAsync(r => r.Id == rule.Id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            rule.Id = rule.Id == Guid.Empty ? Guid.NewGuid() : rule.Id;
            rule.CreatedBy = CurrentActor().Name;
            rule.CreatedAt = DateTimeOffset.UtcNow;
            set.Add(rule);
            existing = rule;
        }
        else
        {
            existing.Name = rule.Name.Trim();
            existing.Description = rule.Description;
            existing.PermissionKeyA = rule.PermissionKeyA;
            existing.PermissionKeyB = rule.PermissionKeyB;
            existing.IsEnabled = rule.IsEnabled;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 提示既有角色是否已違反（規則僅阻擋之後的異動，既有違規需人工處理）
        var conflictingRoles = await FindRolesViolatingAsync(existing, cancellationToken).ConfigureAwait(false);
        var message = conflictingRoles.Count == 0
            ? "規則已儲存。"
            : $"規則已儲存。注意：目前已有角色同時擁有這兩項權限，請檢視並調整：{string.Join("、", conflictingRoles)}";

        logger.LogInformation("[SoD] 已儲存職能分立規則 {Name}：{A} ⟷ {B}", existing.Name, existing.PermissionKeyA, existing.PermissionKeyB);
        return (true, message, existing);
    }

    public async Task<(bool Succeeded, string Message)> DeleteSodRuleAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        var rule = await dbContext.Set<SodRule>().FirstOrDefaultAsync(r => r.Id == ruleId, cancellationToken).ConfigureAwait(false);
        if (rule is null) return (false, "找不到指定的規則。");

        dbContext.Set<SodRule>().Remove(rule);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return (true, "規則已刪除。");
    }

    private async Task<List<string>> FindRolesViolatingAsync(SodRule rule, CancellationToken cancellationToken)
    {
        if (!rule.IsEnabled) return [];

        var result = new List<string>();
        var roles = await roleManager.Roles.AsNoTracking().Where(r => !r.IsDeleted)
            .Select(r => new { r.Id, r.Name }).ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var role in roles)
        {
            var granted = (await permissionService.GetRoleGlobalPermissionsAsync(role.Id.ToString(), cancellationToken).ConfigureAwait(false))
                ?.GrantedPermissionKeys ?? [];
            if (granted.Contains(rule.PermissionKeyA, StringComparer.OrdinalIgnoreCase) &&
                granted.Contains(rule.PermissionKeyB, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(role.Name ?? role.Id.ToString());
            }
        }

        return result;
    }

    private static List<string> Normalize(IEnumerable<string>? keys) =>
        (keys ?? []).Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private (Guid? Id, string Name) CurrentActor()
    {
        var user = httpContextAccessor.HttpContext?.User;
        var raw = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value;
        return (Guid.TryParse(raw, out var id) ? id : null,
                user?.Identity?.Name ?? user?.FindFirst(ClaimTypes.Name)?.Value ?? "未知使用者");
    }
}
