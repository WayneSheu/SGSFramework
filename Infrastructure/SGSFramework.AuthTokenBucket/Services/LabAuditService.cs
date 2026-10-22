using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Identities;

namespace SGSFramework.AuthTokenBucket.Services;

/// <summary>稽核指派狀態</summary>
public static class LabAuditStatuses
{
    public const string Upcoming = "upcoming";
    public const string Active = "active";
    public const string Expired = "expired";
    public const string Revoked = "revoked";
}

public sealed record LabAuditAssignmentDto(
    Guid Id,
    string UserId,
    string UserName,
    string FullName,
    Guid TenantLabId,
    string LabName,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset EffectiveTo,
    string Reason,
    string Status,
    string? AssignedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RevokedAt,
    string? RevokedByName,
    string? RevokeReason);

/// <summary>可被指派稽核的使用者（持有啟用中的稽核角色）</summary>
public sealed record AuditorCandidateDto(string UserId, string UserName, string FullName, string? Email, IReadOnlyList<string> AuditRoles);

public sealed record CreateLabAuditRequest(Guid UserId, Guid TenantLabId, DateTimeOffset EffectiveFrom, DateTimeOffset EffectiveTo, string Reason);

/// <summary>
/// 稽核權限 = 稽核角色（全域權限，<see cref="ApplicationRole.RequiresAuditAssignment"/>）＋ 稽核指派（實驗室 × 起訖期間）。
/// 稽核角色的權限僅於有效指派的實驗室生效；指派期間內該實驗室會加入使用者可切換的實驗室清單。
/// </summary>
public interface ILabAuditService
{
    Task<IReadOnlyList<LabAuditAssignmentDto>> ListAsync(Guid? tenantLabId, Guid? userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditorCandidateDto>> GetAuditorCandidatesAsync(CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message, LabAuditAssignmentDto? Assignment)> CreateAsync(CreateLabAuditRequest request, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message)> RevokeAsync(Guid id, string? reason, CancellationToken cancellationToken = default);

    /// <summary>使用者目前有效的稽核指派（實驗室 → 迄日）</summary>
    Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetActiveLabsAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class LabAuditService(
    DbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IOrganizationIntegrationService orgIntegrationService,
    IPermissionChangeNotifier permissionChangeNotifier,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider,
    ILogger<LabAuditService> logger) : ILabAuditService
{
    /// <summary>單次指派最長期間（避免以稽核指派取代人事歸屬）</summary>
    private static readonly TimeSpan MaxDuration = TimeSpan.FromDays(366);

    public async Task<IReadOnlyList<LabAuditAssignmentDto>> ListAsync(Guid? tenantLabId, Guid? userId, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Set<LabAuditAssignment>().AsNoTracking();
        if (tenantLabId.HasValue) query = query.Where(a => a.TenantLabId == tenantLabId.Value);
        if (userId.HasValue) query = query.Where(a => a.UserId == userId.Value);

        var items = await query.OrderByDescending(a => a.EffectiveFrom).Take(500)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return await ToDtosAsync(items, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AuditorCandidateDto>> GetAuditorCandidatesAsync(CancellationToken cancellationToken = default)
    {
        var auditRoles = await dbContext.Set<ApplicationRole>().AsNoTracking()
            .Where(r => r.RequiresAuditAssignment && !r.IsDeleted && r.Status != RoleStatus.Disabled && r.Name != null)
            .Select(r => r.Name!)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var candidates = new Dictionary<Guid, (ApplicationUser User, List<string> Roles)>();
        foreach (var roleName in auditRoles)
        {
            foreach (var user in await userManager.GetUsersInRoleAsync(roleName).ConfigureAwait(false))
            {
                if (user.IsDeleted) continue;
                if (!candidates.TryGetValue(user.Id, out var entry)) candidates[user.Id] = entry = (user, []);
                entry.Roles.Add(roleName);
            }
        }

        return candidates.Values
            .OrderBy(c => c.User.FullName, StringComparer.CurrentCulture)
            .Select(c => new AuditorCandidateDto(c.User.Id.ToString(), c.User.UserName ?? string.Empty, c.User.FullName, c.User.Email, c.Roles))
            .ToList();
    }

    public async Task<(bool Succeeded, string Message, LabAuditAssignmentDto? Assignment)> CreateAsync(
        CreateLabAuditRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.TenantLabId == Guid.Empty) return (false, "實驗室識別碼無效。", null);
        if (string.IsNullOrWhiteSpace(request.Reason)) return (false, "請填寫稽核原因或稽核案號。", null);
        if (request.EffectiveTo <= request.EffectiveFrom) return (false, "迄日必須晚於起日。", null);
        if (request.EffectiveTo - request.EffectiveFrom > MaxDuration) return (false, "單次稽核指派期間不可超過一年。", null);

        var now = timeProvider.GetUtcNow();
        if (request.EffectiveTo < now) return (false, "迄日已過，無法建立已失效的指派。", null);

        var user = await userManager.FindByIdAsync(request.UserId.ToString()).ConfigureAwait(false);
        if (user is null || user.IsDeleted) return (false, "找不到指定的使用者。", null);

        // 須持有稽核角色：指派本身不授予權限，權限來自稽核角色
        var candidates = await GetAuditorCandidatesAsync(cancellationToken).ConfigureAwait(false);
        if (candidates.All(c => c.UserId != user.Id.ToString()))
        {
            return (false, $"使用者「{Display(user)}」未持有稽核角色（例：各區實驗室稽核員），請先於角色管理指派。", null);
        }

        var lab = await orgIntegrationService.GetOrganizationByIdAsync(request.TenantLabId, cancellationToken).ConfigureAwait(false);
        if (lab is null) return (false, "找不到指定的區域實驗室。", null);
        if (!lab.IsActive) return (false, $"實驗室「{lab.Name}」已停用，無法指派稽核。", null);

        bool overlaps = await dbContext.Set<LabAuditAssignment>().AsNoTracking()
            .AnyAsync(a => a.UserId == user.Id && a.TenantLabId == request.TenantLabId && a.RevokedAt == null &&
                           a.EffectiveFrom <= request.EffectiveTo && a.EffectiveTo >= request.EffectiveFrom, cancellationToken)
            .ConfigureAwait(false);
        if (overlaps) return (false, "此使用者於該實驗室已有期間重疊的稽核指派，請調整期間或先撤銷既有指派。", null);

        var (actorId, actorName) = CurrentActor();
        var entity = new LabAuditAssignment
        {
            UserId = user.Id,
            TenantLabId = request.TenantLabId,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Reason = request.Reason.Trim(),
            AssignedBy = actorId,
            AssignedByName = actorName,
            CreatedAt = now
        };
        dbContext.Set<LabAuditAssignment>().Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await permissionChangeNotifier.NotifyUsersAsync([user.Id.ToString()], "lab-audit-assigned", cancellationToken).ConfigureAwait(false);
        logger.LogInformation("[LabAudit] {Actor} 指派 {User} 稽核實驗室 {Lab}：{From:yyyy-MM-dd} ～ {To:yyyy-MM-dd}（{Reason}）",
            actorName, user.UserName, lab.Name, entity.EffectiveFrom, entity.EffectiveTo, entity.Reason);

        var dto = (await ToDtosAsync([entity], cancellationToken).ConfigureAwait(false))[0];
        return (true, $"已指派「{Display(user)}」稽核「{lab.Name}」。", dto);
    }

    public async Task<(bool Succeeded, string Message)> RevokeAsync(Guid id, string? reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return (false, "請填寫撤銷原因。");

        var entity = await dbContext.Set<LabAuditAssignment>().FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is null) return (false, "找不到指定的稽核指派。");
        if (entity.RevokedAt is not null) return (false, "此稽核指派已撤銷。");

        var now = timeProvider.GetUtcNow();
        if (entity.EffectiveTo < now) return (false, "此稽核指派已到期，無須撤銷。");

        var (_, actorName) = CurrentActor();
        entity.RevokedAt = now;
        entity.RevokedByName = actorName;
        entity.RevokeReason = reason.Trim();
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await permissionChangeNotifier.NotifyUsersAsync([entity.UserId.ToString()], "lab-audit-revoked", cancellationToken).ConfigureAwait(false);
        logger.LogInformation("[LabAudit] {Actor} 撤銷稽核指派 {Id}（{Reason}）", actorName, id, entity.RevokeReason);
        return (true, "已撤銷稽核指派，該使用者於此實驗室的稽核權限立即失效。");
    }

    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetActiveLabsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var active = await dbContext.Set<LabAuditAssignment>().AsNoTracking()
            .Where(a => a.UserId == userId && a.RevokedAt == null && a.EffectiveFrom <= now && a.EffectiveTo >= now)
            .Select(a => new { a.TenantLabId, a.EffectiveTo })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return active.GroupBy(a => a.TenantLabId).ToDictionary(g => g.Key, g => g.Max(a => a.EffectiveTo));
    }

    private async Task<List<LabAuditAssignmentDto>> ToDtosAsync(IReadOnlyCollection<LabAuditAssignment> items, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var userIds = items.Select(i => i.UserId).Distinct().ToList();
        var users = await dbContext.Set<ApplicationUser>().AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.FullName })
            .ToDictionaryAsync(u => u.Id, cancellationToken).ConfigureAwait(false);

        var labNames = new Dictionary<Guid, string>();
        foreach (var labId in items.Select(i => i.TenantLabId).Distinct())
        {
            var org = await orgIntegrationService.GetOrganizationByIdAsync(labId, cancellationToken).ConfigureAwait(false);
            labNames[labId] = org?.Name ?? labId.ToString()[..8];
        }

        return items.Select(a =>
        {
            users.TryGetValue(a.UserId, out var u);
            var status = a.RevokedAt is not null ? LabAuditStatuses.Revoked
                : a.EffectiveTo < now ? LabAuditStatuses.Expired
                : a.EffectiveFrom > now ? LabAuditStatuses.Upcoming
                : LabAuditStatuses.Active;
            return new LabAuditAssignmentDto(
                a.Id, a.UserId.ToString(), u?.UserName ?? string.Empty, u?.FullName ?? string.Empty,
                a.TenantLabId, labNames[a.TenantLabId], a.EffectiveFrom, a.EffectiveTo, a.Reason, status,
                a.AssignedByName, a.CreatedAt, a.RevokedAt, a.RevokedByName, a.RevokeReason);
        }).ToList();
    }

    private static string Display(ApplicationUser user) => string.IsNullOrWhiteSpace(user.FullName) ? user.UserName ?? string.Empty : user.FullName;

    private (Guid? Id, string Name) CurrentActor()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        var raw = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal?.FindFirst("sub")?.Value;
        return (Guid.TryParse(raw, out var id) ? id : null,
                principal?.Identity?.Name ?? principal?.FindFirst(ClaimTypes.Name)?.Value ?? "未知使用者");
    }
}
