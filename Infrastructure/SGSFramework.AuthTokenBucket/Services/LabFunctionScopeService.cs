using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using ZiggyCreatures.Caching.Fusion;

namespace SGSFramework.AuthTokenBucket.Services;

/// <summary>區域實驗室功能開放範圍（檢視 / 設定用）</summary>
public sealed record LabFunctionScopeDto(
    Guid TenantLabId,
    bool IsRestricted,
    IReadOnlyList<string> PermissionKeys,
    DateTimeOffset? UpdatedAt,
    string? UpdatedByName);

/// <summary>
/// 區域實驗室功能開放範圍：作為該實驗室內所有使用者（含全域角色、直接權限）的權限上限。
/// 未設定或未啟用限制時，所有功能開放；系統管理員不受限制（於 UserRuntimeScopeService 先行放行）。
/// </summary>
public interface ILabFunctionScopeService
{
    Task<LabFunctionScopeDto> GetAsync(Guid tenantLabId, CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string Message)> SaveAsync(Guid tenantLabId, bool isRestricted, IEnumerable<string>? permissionKeys, CancellationToken cancellationToken = default);

    /// <summary>實驗室允許的權限鍵；null 表示不限制</summary>
    Task<IReadOnlySet<string>?> GetAllowedKeysAsync(Guid tenantLabId, CancellationToken cancellationToken = default);

    /// <summary>依實驗室上限過濾權限鍵（不限制時原樣回傳）</summary>
    Task<IReadOnlyCollection<string>> FilterAsync(Guid tenantLabId, IEnumerable<string> permissionKeys, CancellationToken cancellationToken = default);
}

public sealed class LabFunctionScopeService(
    DbContext dbContext,
    IFusionCache cache,
    IPermissionRegistry permissionRegistry,
    IPermissionChangeNotifier permissionChangeNotifier,
    IHttpContextAccessor httpContextAccessor,
    ILogger<LabFunctionScopeService> logger) : ILabFunctionScopeService
{
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(15);

    private static string CacheKey(Guid tenantLabId) => $"lab-func-scope:{tenantLabId}";

    /// <summary>快取值：未限制以空陣列 + 旗標表示（FusionCache 不快取 null 時仍可區分）</summary>
    private sealed record CachedScope(bool IsRestricted, string[] Keys);

    public async Task<LabFunctionScopeDto> GetAsync(Guid tenantLabId, CancellationToken cancellationToken = default)
    {
        var policy = await dbContext.Set<LabFunctionPolicy>().AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantLabId == tenantLabId, cancellationToken).ConfigureAwait(false);

        var keys = await dbContext.Set<LabFunctionScope>().AsNoTracking()
            .Where(s => s.TenantLabId == tenantLabId)
            .Select(s => s.PermissionKey)
            .OrderBy(k => k)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new LabFunctionScopeDto(tenantLabId, policy?.IsRestricted ?? false, keys, policy?.UpdatedAt, policy?.UpdatedByName);
    }

    public async Task<(bool Succeeded, string Message)> SaveAsync(
        Guid tenantLabId, bool isRestricted, IEnumerable<string>? permissionKeys, CancellationToken cancellationToken = default)
    {
        if (tenantLabId == Guid.Empty) return (false, "實驗室識別碼無效。");

        var registered = permissionRegistry.GetAllMappings().Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requested = (permissionKeys ?? [])
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var unknown = requested.Where(k => !registered.Contains(k)).ToList();
        if (unknown.Count > 0)
        {
            return (false, $"包含未註冊的權限：{string.Join("、", unknown.Take(5))}{(unknown.Count > 5 ? " 等" : string.Empty)}");
        }

        if (isRestricted && requested.Count == 0)
        {
            return (false, "啟用功能限制時至少須開放一項功能；若要全部開放請關閉限制。");
        }

        var (actorId, actorName) = CurrentActor();

        var policy = await dbContext.Set<LabFunctionPolicy>()
            .FirstOrDefaultAsync(p => p.TenantLabId == tenantLabId, cancellationToken).ConfigureAwait(false);
        if (policy is null)
        {
            policy = new LabFunctionPolicy { TenantLabId = tenantLabId };
            dbContext.Set<LabFunctionPolicy>().Add(policy);
        }

        policy.IsRestricted = isRestricted;
        policy.UpdatedAt = DateTimeOffset.UtcNow;
        policy.UpdatedBy = actorId;
        policy.UpdatedByName = actorName;

        // 關閉限制時保留既有勾選，方便日後再次啟用
        var scopeSet = dbContext.Set<LabFunctionScope>();
        var existing = await scopeSet.Where(s => s.TenantLabId == tenantLabId).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (isRestricted || requested.Count > 0)
        {
            var target = requested.ToHashSet(StringComparer.OrdinalIgnoreCase);
            scopeSet.RemoveRange(existing.Where(e => !target.Contains(e.PermissionKey)));
            scopeSet.AddRange(target.Where(k => existing.All(e => !string.Equals(e.PermissionKey, k, StringComparison.OrdinalIgnoreCase)))
                .Select(k => new LabFunctionScope { TenantLabId = tenantLabId, PermissionKey = k }));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await cache.RemoveAsync(CacheKey(tenantLabId), token: cancellationToken).ConfigureAwait(false);

        // 通知該實驗室成員重新載入權限與選單
        var userIds = await dbContext.Set<UserLabMapping>().AsNoTracking()
            .Where(m => m.TenantLabId == tenantLabId && m.IsActive)
            .Select(m => m.UserId.ToString())
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        await permissionChangeNotifier.NotifyUsersAsync(userIds, "lab-function-scope-changed", cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[LabFunctionScope] {Actor} 更新實驗室 {LabId} 功能開放範圍：限制={Restricted}，開放 {Count} 項，影響 {Users} 位使用者",
            actorName, tenantLabId, isRestricted, requested.Count, userIds.Count);

        return (true, isRestricted
            ? $"已更新：此實驗室僅開放 {requested.Count} 項功能權限，影響 {userIds.Count} 位使用者。"
            : "已更新：此實驗室開放所有功能。");
    }

    public async Task<IReadOnlySet<string>?> GetAllowedKeysAsync(Guid tenantLabId, CancellationToken cancellationToken = default)
    {
        var scope = await cache.GetOrSetAsync<CachedScope>(
            CacheKey(tenantLabId),
            async (_, ct) =>
            {
                var dto = await GetAsync(tenantLabId, ct).ConfigureAwait(false);
                return new CachedScope(dto.IsRestricted, dto.PermissionKeys.ToArray());
            },
            CacheExpiration,
            token: cancellationToken).ConfigureAwait(false);

        return scope is { IsRestricted: true }
            ? scope.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;
    }

    public async Task<IReadOnlyCollection<string>> FilterAsync(Guid tenantLabId, IEnumerable<string> permissionKeys, CancellationToken cancellationToken = default)
    {
        var keys = permissionKeys as IReadOnlyCollection<string> ?? permissionKeys.ToList();
        var allowed = await GetAllowedKeysAsync(tenantLabId, cancellationToken).ConfigureAwait(false);
        return allowed is null ? keys : keys.Where(allowed.Contains).ToList();
    }

    private (Guid? Id, string Name) CurrentActor()
    {
        var user = httpContextAccessor.HttpContext?.User;
        var raw = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user?.FindFirst("sub")?.Value;
        return (Guid.TryParse(raw, out var id) ? id : null,
                user?.Identity?.Name ?? user?.FindFirst(ClaimTypes.Name)?.Value ?? "未知使用者");
    }
}
