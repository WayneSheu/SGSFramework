using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using SGSFramework.Core.Abstractions.Permissions.Identities;

namespace SGSFramework.AuthTokenBucket.Services;

/// <summary>
/// 計算使用者經由「角色」取得的有效權限，供執行期權限檢查與使用者直接權限合併。
/// 角色狀態規則：
/// - 啟用中 (Active)：授予權限
/// - 已鎖定 (Locked)：既有成員保留權限（新成員與權限異動已於管理端阻擋）
/// - 已停用 (Disabled)：不授予任何權限，既有成員立即失去此角色的權限
/// </summary>
public interface IRoleEffectivePermissionProvider
{
    /// <summary>全域角色權限，格式與使用者全域權限相同：「模組鍵:位元遮罩」</summary>
    Task<IReadOnlyList<string>> GetGlobalRolePermissionsAsync(ApplicationUser user, CancellationToken cancellationToken = default);

    /// <summary>指定實驗室的角色權限（全域角色權限 + 該實驗室的角色授權），回傳「模組鍵:位元遮罩」或完整權限鍵</summary>
    Task<IReadOnlyList<string>> GetLabRolePermissionsAsync(ApplicationUser user, Guid labId, CancellationToken cancellationToken = default);
}

public sealed class RoleEffectivePermissionProvider(
    DbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IPermissionRegistry permissionRegistry,
    ILogger<RoleEffectivePermissionProvider> logger) : IRoleEffectivePermissionProvider
{
    private readonly DbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    private readonly UserManager<ApplicationUser> _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
    private readonly IPermissionRegistry _permissionRegistry = permissionRegistry ?? throw new ArgumentNullException(nameof(permissionRegistry));
    private readonly ILogger<RoleEffectivePermissionProvider> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<IReadOnlyList<string>> GetGlobalRolePermissionsAsync(ApplicationUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var roleIds = await GetEffectiveRoleIdsAsync(user, cancellationToken).ConfigureAwait(false);
        return await LoadGlobalRolePermissionsAsync(roleIds, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetLabRolePermissionsAsync(ApplicationUser user, Guid labId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var roleIds = await GetEffectiveRoleIdsAsync(user, cancellationToken).ConfigureAwait(false);
        if (roleIds.Count == 0) return [];

        // 全域角色權限於任何實驗室範圍內皆有效
        var result = new List<string>(await LoadGlobalRolePermissionsAsync(roleIds, cancellationToken).ConfigureAwait(false));

        // 實驗室層級角色授權（位元向量，位元位置為權限註冊表的全域索引）
        var vectors = await _dbContext.Set<PermissionGrant>()
            .AsNoTracking()
            .Where(g => g.LabId == labId && roleIds.Contains(g.RoleId))
            .Select(g => g.PermissionVector)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (vectors.Count > 0)
        {
            var bitToKey = _permissionRegistry.GetAllMappings()
                .GroupBy(m => m.Value)
                .ToDictionary(g => g.Key, g => g.First().Key);

            foreach (var vector in vectors)
            {
                for (int byteIdx = 0; byteIdx < vector.Length; byteIdx++)
                {
                    byte b = vector[byteIdx];
                    if (b == 0) continue;

                    for (int bitIdx = 0; bitIdx < 8; bitIdx++)
                    {
                        if ((b & (1 << bitIdx)) != 0 && bitToKey.TryGetValue((byteIdx * 8) + bitIdx, out var key))
                        {
                            result.Add(key); // 完整權限鍵（ParsePermissionKeys 會直接採用）
                        }
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 取得使用者所屬且「未停用、未刪除」的角色 ID
    /// </summary>
    private async Task<List<Guid>> GetEffectiveRoleIdsAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var roleNames = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        if (roleNames.Count == 0) return [];

        var roles = await _dbContext.Set<ApplicationRole>()
            .AsNoTracking()
            .Where(r => r.Name != null && roleNames.Contains(r.Name) && !r.IsDeleted)
            .Select(r => new { r.Id, r.Name, r.Status, r.EffectiveFrom, r.EffectiveTo })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // 停用角色、或不在生效期間內（尚未生效 / 已過期）的角色不授予權限
        var now = DateTimeOffset.UtcNow;
        bool IsEffective(DateTimeOffset? from, DateTimeOffset? to) =>
            (!from.HasValue || from.Value <= now) && (!to.HasValue || to.Value >= now);

        var excluded = roles.Where(r => r.Status == RoleStatus.Disabled || !IsEffective(r.EffectiveFrom, r.EffectiveTo)).ToList();
        if (excluded.Count > 0)
        {
            _logger.LogDebug("[RolePermission] 使用者 {UserId} 的停用或非生效期間角色不計入權限：{Roles}",
                user.Id, string.Join(", ", excluded.Select(r => r.Name)));
        }

        return roles.Except(excluded).Select(r => r.Id).ToList();
    }

    /// <summary>
    /// 讀取角色全域權限，同一模組鍵於多個角色間以位元 OR 合併
    /// </summary>
    private async Task<IReadOnlyList<string>> LoadGlobalRolePermissionsAsync(List<Guid> roleIds, CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0) return [];

        var roleIdStrings = roleIds.Select(id => id.ToString()).ToList();

        var records = await _dbContext.Set<RoleGlobalPermission>()
            .AsNoTracking()
            .Where(p => roleIdStrings.Contains(p.RoleId))
            .Select(p => new { p.PermissionKey, p.Bitmask })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return records
            .GroupBy(r => r.PermissionKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => $"{g.Key}:{g.Aggregate(0L, (mask, r) => mask | r.Bitmask)}")
            .ToList();
    }
}
