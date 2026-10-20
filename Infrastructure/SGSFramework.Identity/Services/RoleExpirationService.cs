using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Entities.Identities;

namespace SGSFramework.Identity.Services;

/// <summary>
/// 過渡期角色管理：設定 / 清除角色指派的有效期限，並回收已到期的指派。
/// </summary>
public interface IRoleExpirationService
{
    /// <summary>設定期限；expiresAt 為 null 時改為永久（清除期限）。僅作用於該角色的現有成員。</summary>
    Task<(bool Succeeded, string Message)> SetExpirationAsync(Guid roleId, IEnumerable<Guid> userIds, DateTimeOffset? expiresAt, string? reason, CancellationToken cancellationToken = default);

    Task ClearAsync(Guid roleId, IEnumerable<Guid> userIds, CancellationToken cancellationToken = default);

    Task<Dictionary<Guid, UserRoleExpiration>> GetByRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>回收所有已到期的角色指派，回傳回收筆數</summary>
    Task<int> ProcessDueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 角色生效期間起訖點已在 (since, now] 內跨越者：清除成員權限快取並通知前端，使權限準時生效 / 失效
    /// </summary>
    Task<int> ProcessEffectivePeriodTransitionsAsync(DateTimeOffset since, CancellationToken cancellationToken = default);
}

public sealed class RoleExpirationService(
    DbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IPermissionChangeNotifier permissionChangeNotifier,
    IRoleAuditService roleAuditService,
    IHttpContextAccessor httpContextAccessor,
    ILogger<RoleExpirationService> logger) : IRoleExpirationService
{
    public async Task<(bool Succeeded, string Message)> SetExpirationAsync(
        Guid roleId, IEnumerable<Guid> userIds, DateTimeOffset? expiresAt, string? reason, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return (false, "未指定使用者。");

        if (expiresAt is { } exp && exp <= DateTimeOffset.UtcNow)
        {
            return (false, "到期時間必須晚於現在。");
        }

        var role = await roleManager.FindByIdAsync(roleId.ToString()).ConfigureAwait(false);
        if (role?.Name is null || role.IsDeleted) return (false, "找不到指定的角色。");

        // 僅允許對現有成員設定期限
        var memberIds = (await userManager.GetUsersInRoleAsync(role.Name).ConfigureAwait(false)).Select(u => u.Id).ToHashSet();
        var notMembers = ids.Where(id => !memberIds.Contains(id)).ToList();
        if (notMembers.Count > 0) return (false, $"有 {notMembers.Count} 位使用者不是此角色的成員，無法設定期限。");

        var existing = await dbContext.Set<UserRoleExpiration>()
            .Where(x => x.RoleId == roleId && ids.Contains(x.UserId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (expiresAt is null)
        {
            dbContext.Set<UserRoleExpiration>().RemoveRange(existing);
        }
        else
        {
            var actor = CurrentActorName();
            foreach (var userId in ids)
            {
                var row = existing.FirstOrDefault(x => x.UserId == userId);
                if (row is null)
                {
                    dbContext.Set<UserRoleExpiration>().Add(new UserRoleExpiration
                    {
                        UserId = userId, RoleId = roleId, ExpiresAt = expiresAt.Value, Reason = reason, CreatedBy = actor
                    });
                }
                else
                {
                    row.ExpiresAt = expiresAt.Value;
                    row.Reason = reason;
                    row.CreatedBy = actor;
                    row.CreatedAt = DateTimeOffset.UtcNow;
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var userNames = await userManager.Users.Where(u => ids.Contains(u.Id)).Select(u => u.UserName).ToListAsync(cancellationToken).ConfigureAwait(false);
        var summary = expiresAt is null
            ? $"取消過渡期，改為永久成員：{string.Join("、", userNames)}"
            : $"設定過渡期至 {expiresAt.Value.ToLocalTime():yyyy-MM-dd HH:mm}，到期自動回收：{string.Join("、", userNames)}" +
              (string.IsNullOrWhiteSpace(reason) ? string.Empty : $"（原因：{reason}）");

        await roleAuditService.RecordAsync(roleId, role.Name, RoleAuditActions.Updated, summary, ids.Count,
            new { UserIds = ids, ExpiresAt = expiresAt, Reason = reason }, cancellationToken).ConfigureAwait(false);

        return (true, expiresAt is null ? "已改為永久成員。" : "已設定過渡期。");
    }

    public async Task ClearAsync(Guid roleId, IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var rows = await dbContext.Set<UserRoleExpiration>()
            .Where(x => x.RoleId == roleId && ids.Contains(x.UserId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (rows.Count == 0) return;
        dbContext.Set<UserRoleExpiration>().RemoveRange(rows);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Dictionary<Guid, UserRoleExpiration>> GetByRoleAsync(Guid roleId, CancellationToken cancellationToken = default) =>
        await dbContext.Set<UserRoleExpiration>()
            .AsNoTracking()
            .Where(x => x.RoleId == roleId)
            .ToDictionaryAsync(x => x.UserId, cancellationToken).ConfigureAwait(false);

    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var due = await dbContext.Set<UserRoleExpiration>()
            .Where(x => x.ExpiresAt <= now)
            .OrderBy(x => x.ExpiresAt)
            .Take(200)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        int revoked = 0;
        foreach (var item in due)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var role = await roleManager.FindByIdAsync(item.RoleId.ToString()).ConfigureAwait(false);
            var user = await userManager.FindByIdAsync(item.UserId.ToString()).ConfigureAwait(false);

            if (role?.Name is not null && user is not null && await userManager.IsInRoleAsync(user, role.Name).ConfigureAwait(false))
            {
                var result = await userManager.RemoveFromRoleAsync(user, role.Name).ConfigureAwait(false);
                if (!result.Succeeded)
                {
                    logger.LogWarning("[RoleExpiration] 回收到期角色失敗，下次排程重試。User: {User}, Role: {Role}, Errors: {Errors}",
                        user.UserName, role.Name, string.Join("; ", result.Errors.Select(e => e.Description)));
                    continue;
                }

                revoked++;
                await roleAuditService.RecordAsync(item.RoleId, role.Name, RoleAuditActions.MemberExpired,
                    $"過渡期到期，自動解除 {user.UserName} 的角色指派" +
                    (string.IsNullOrWhiteSpace(item.Reason) ? string.Empty : $"（原因：{item.Reason}）"),
                    1, new { UserId = item.UserId, item.ExpiresAt, SetBy = item.CreatedBy }, cancellationToken).ConfigureAwait(false);
                await permissionChangeNotifier.NotifyUsersAsync([item.UserId.ToString()], "role-expired", cancellationToken).ConfigureAwait(false);
            }

            // 已回收或已非成員：期限紀錄完成任務
            dbContext.Set<UserRoleExpiration>().Remove(item);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        if (revoked > 0) logger.LogInformation("[RoleExpiration] 已自動回收 {Count} 筆到期的角色指派。", revoked);
        return revoked;
    }

    public async Task<int> ProcessEffectivePeriodTransitionsAsync(DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var roles = await roleManager.Roles.AsNoTracking()
            .Where(r => !r.IsDeleted &&
                        ((r.EffectiveFrom > since && r.EffectiveFrom <= now) || (r.EffectiveTo > since && r.EffectiveTo <= now)))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var role in roles.Where(r => r.Name is not null))
        {
            bool started = role.EffectiveFrom > since && role.EffectiveFrom <= now;
            var memberCount = (await userManager.GetUsersInRoleAsync(role.Name!).ConfigureAwait(false)).Count;

            await permissionChangeNotifier.NotifyRoleMembersAsync(role.Name!, started ? "role-effective-started" : "role-effective-ended", cancellationToken).ConfigureAwait(false);
            await roleAuditService.RecordAsync(role.Id, role.Name!, RoleAuditActions.StatusChanged,
                started ? $"角色進入生效期間，{memberCount} 位成員開始取得此角色權限" : $"角色生效期間結束，{memberCount} 位成員失去此角色權限",
                memberCount, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return roles.Count;
    }

    private string CurrentActorName()
    {
        var user = httpContextAccessor.HttpContext?.User;
        return user?.Identity?.Name ?? user?.FindFirst(ClaimTypes.Name)?.Value ?? "系統";
    }
}

/// <summary>
/// 過渡期角色到期回收排程：啟動後每 5 分鐘執行一次。
/// </summary>
public sealed class RoleExpirationWorker(IServiceScopeFactory scopeFactory, ILogger<RoleExpirationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 延後首次執行，避開啟動期間的資料庫遷移與種子資料
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(Interval);
        var lastRun = DateTimeOffset.UtcNow - Interval;
        do
        {
            try
            {
                var runAt = DateTimeOffset.UtcNow;
                await using var scope = scopeFactory.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IRoleExpirationService>();
                await service.ProcessDueAsync(stoppingToken).ConfigureAwait(false);
                await service.ProcessEffectivePeriodTransitionsAsync(lastRun, stoppingToken).ConfigureAwait(false);
                lastRun = runAt;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[RoleExpiration] 回收到期角色指派時發生例外，將於下次排程重試。");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}
