#nullable enable

namespace SGSFramework.AuthTokenBucket.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.DTOs.PermissionGrants;
using SGSFramework.AuthTokenBucket.DTOs.PermissionTree;
using SGSFramework.AuthTokenBucket.DTOs.RolePermissions;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

public class PermissionManagementService<TContext, TRole, TKey> : IPermissionManagementService
    where TContext : DbContext
    where TRole : IdentityRole<TKey>
    where TKey : IEquatable<TKey>
{
    private readonly TContext _dbContext;
    private readonly RoleManager<TRole> _roleManager;
    private readonly IPermissionRegistry _permissionRegistry;
    private readonly ILogger<PermissionManagementService<TContext, TRole, TKey>> _logger;

    public PermissionManagementService(
        TContext dbContext,
        RoleManager<TRole> roleManager,
        IPermissionRegistry permissionRegistry,
        ILogger<PermissionManagementService<TContext, TRole, TKey>> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _permissionRegistry = permissionRegistry ?? throw new ArgumentNullException(nameof(permissionRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> HasPermissionAsync(string userId, string permissionCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);
        await Task.CompletedTask.ConfigureAwait(false);
        return true;
    }

    public async Task GrantPermissionToRoleAsync(string roleId, string permissionCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

        var role = await _roleManager.FindByIdAsync(roleId).ConfigureAwait(false);
        if (role == null) return;

        var claims = await _roleManager.GetClaimsAsync(role).ConfigureAwait(false);
        if (!claims.Any(c => c.Type == "Permission" && c.Value.Equals(permissionCode, StringComparison.OrdinalIgnoreCase)))
        {
            await _roleManager.AddClaimAsync(role, new Claim("Permission", permissionCode)).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RevokePermissionFromRoleAsync(string roleId, string permissionCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

        var role = await _roleManager.FindByIdAsync(roleId).ConfigureAwait(false);
        if (role == null) return;

        var claims = await _roleManager.GetClaimsAsync(role).ConfigureAwait(false);
        var targetClaim = claims.FirstOrDefault(c => c.Type == "Permission" && c.Value.Equals(permissionCode, StringComparison.OrdinalIgnoreCase));
        if (targetClaim != null)
        {
            await _roleManager.RemoveClaimAsync(role, targetClaim).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 同步並更新系統 PermissionMetadata 資料表，並透過 DTO 投影回傳同步後的完整資料清單
    /// </summary>
    public async Task<IReadOnlyCollection<PermissionMetadataDto>> SyncPermissionMetadataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var registryPermissions = _permissionRegistry.GetAllPermissions();
            if (registryPermissions == null || !registryPermissions.Any())
            {
                _logger.LogWarning("從權限註冊表中未取得任何權限定義，略過同步。");
                return Array.Empty<PermissionMetadataDto>();
            }

            var distinctRegistryPermissions = registryPermissions
                .GroupBy(p => new {
                    PermissionKey = p.PermissionKey.Trim(),
                    ControllerName = (p.ControllerName ?? string.Empty).Trim(),
                    ActionName = (p.ActionName ?? string.Empty).Trim()
                })
                .Select(g => g.First())
                .ToList();

            var dbSet = _dbContext.Set<PermissionMetadata>();
            var existingItems = await dbSet.ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var reg in distinctRegistryPermissions)
            {
                var normalizedController = reg.ControllerName ?? string.Empty;
                var normalizedAction = reg.ActionName ?? string.Empty;

                var existing = existingItems.FirstOrDefault(p =>
                    p.PermissionKey.Equals(reg.PermissionKey, StringComparison.OrdinalIgnoreCase) &&
                    (p.ControllerName ?? string.Empty).Equals(normalizedController, StringComparison.OrdinalIgnoreCase) &&
                    (p.ActionName ?? string.Empty).Equals(normalizedAction, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    existing.ModuleName = reg.ModuleName;
                    existing.ModuleTitle = !string.IsNullOrEmpty(reg.ModuleTitle) ? reg.ModuleTitle : existing.ModuleTitle;
                    existing.ControllerName = normalizedController;
                    existing.ControllerTitle = !string.IsNullOrEmpty(reg.ControllerTitle) ? reg.ControllerTitle : existing.ControllerTitle;
                    existing.ActionName = normalizedAction;
                    existing.ActionTitle = !string.IsNullOrEmpty(reg.ActionTitle) ? reg.ActionTitle : existing.ActionTitle;
                    existing.PermissionTitle = !string.IsNullOrEmpty(reg.PermissionTitle) ? reg.PermissionTitle : existing.PermissionTitle;
                    existing.Description = !string.IsNullOrEmpty(reg.Description) ? reg.Description : existing.Description;
                    existing.BitPosition = reg.BitPosition;
                }
                else
                {
                    await dbSet.AddAsync(new PermissionMetadata
                    {
                        ModuleName = reg.ModuleName,
                        ModuleTitle = reg.ModuleTitle ?? reg.ModuleName,
                        ControllerName = normalizedController,
                        ControllerTitle = reg.ControllerTitle ?? normalizedController,
                        ActionName = normalizedAction,
                        ActionTitle = reg.ActionTitle ?? reg.PermissionKey,
                        PermissionTitle = reg.PermissionTitle ?? reg.ActionTitle ?? reg.PermissionKey,
                        PermissionKey = reg.PermissionKey,
                        Description = reg.Description ?? reg.PermissionKey,
                        BitPosition = reg.BitPosition
                    }, cancellationToken).ConfigureAwait(false);
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // 採用 DTO 投影，直接在資料庫端轉換並過濾無關屬性，避免實體追蹤開銷與循環參考
            var updatedList = await dbSet
                .AsNoTracking()
                .OrderBy(p => p.ModuleName)
                .ThenBy(p => p.ControllerName)
                .ThenBy(p => p.BitPosition)
                .Select(p => new PermissionMetadataDto(
                    p.Id,
                    p.ModuleName,
                    p.ModuleTitle,
                    p.ControllerName,
                    p.ControllerTitle,
                    p.ActionName,
                    p.ActionTitle,
                    p.PermissionKey,
                    p.PermissionTitle,
                    p.Description,
                    p.BitPosition
                ))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("成功同步系統 PermissionMetadata 投影資料，共處理筆數: {Count}", updatedList.Count);
            return updatedList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "同步系統 PermissionMetadata 時發生例外。");
            throw;
        }
    }

    /// <summary>
    /// 取得完整系統與動態模組權限清單 (階層式：Section -> Module -> Function(Controller) -> ReadPermission + ActionPermissions)
    /// </summary>
    public async Task<List<PermissionModuleDto>> GetPermissionTreeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dbSet = _dbContext.Set<PermissionMetadata>();

            var flatMetadata = await dbSet
                .AsNoTracking()
                .OrderBy(p => p.ModuleName)
                .ThenBy(p => p.ControllerName)
                .ThenBy(p => p.BitPosition)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (flatMetadata == null || !flatMetadata.Any())
            {
                _logger.LogWarning("資料庫中無任何 PermissionMetadata 資料，權限樹為空。");
                return new List<PermissionModuleDto>();
            }

            var moduleGroups = flatMetadata.GroupBy(p => p.ModuleName);
            var moduleDtos = new List<PermissionModuleDto>();

            foreach (var modGroup in moduleGroups)
            {
                var firstMod = modGroup.First();
                var controllerGroups = modGroup.GroupBy(p => p.ControllerName);
                var functionDtos = new List<PermissionFunctionDto>();

                foreach (var ctrlGroup in controllerGroups)
                {
                    var firstCtrl = ctrlGroup.First();

                    // 1. 濾除重複的 PermissionKey
                    var distinctPermissions = ctrlGroup
                        .GroupBy(p => p.PermissionKey, StringComparer.OrdinalIgnoreCase)
                        .Select(g => g.First())
                        .ToList();

                    // 2. 識別並分離 READ 權限 (依據 Action 名稱慣例或 BitPosition 判定)
                    var readPermEntity = distinctPermissions.FirstOrDefault(p =>
                        p.ActionName.StartsWith("Get", StringComparison.OrdinalIgnoreCase) ||
                        p.ActionName.StartsWith("Read", StringComparison.OrdinalIgnoreCase) ||
                        p.ActionName.StartsWith("List", StringComparison.OrdinalIgnoreCase) ||
                        p.ActionName.StartsWith("Query", StringComparison.OrdinalIgnoreCase) ||
                        p.ActionName.StartsWith("Find", StringComparison.OrdinalIgnoreCase));

                    // 若找不到明顯的 Read 關鍵字，則退而求其次取 BitPosition 最小者作為 Read 權限
                    readPermEntity ??= distinctPermissions.OrderBy(p => p.BitPosition).FirstOrDefault();

                    PermissionDto? readPermissionDto = null;
                    var actionPermissionDtos = new List<PermissionDto>();

                    if (readPermEntity != null)
                    {
                        readPermissionDto = new PermissionDto
                        {
                            PermissionKey = readPermEntity.PermissionKey,
                            PermissionTitle = readPermEntity.PermissionTitle,
                            ActionName = readPermEntity.ActionName,
                            ActionTitle = readPermEntity.ActionTitle,
                            Description = readPermEntity.Description,
                            BitPosition = readPermEntity.BitPosition
                        };
                    }

                    // 3. 其餘權限歸類為 ActionPermissions (Create, Update, Delete 等)
                    foreach (var p in distinctPermissions.Where(p => p != readPermEntity))
                    {
                        actionPermissionDtos.Add(new PermissionDto
                        {
                            PermissionKey = p.PermissionKey,
                            PermissionTitle = p.PermissionTitle,
                            ActionName = p.ActionName,
                            ActionTitle = p.ActionTitle,
                            Description = p.Description,
                            BitPosition = p.BitPosition
                        });
                    }

                    functionDtos.Add(new PermissionFunctionDto
                    {
                        FunctionName = firstCtrl.ControllerName,
                        FunctionTitle = firstCtrl.ControllerTitle,
                        ReadPermission = readPermissionDto,
                        ActionPermissions = actionPermissionDtos
                    });
                }

                moduleDtos.Add(new PermissionModuleDto
                {
                    ModuleName = modGroup.Key,
                    ModuleTitle = firstMod.ModuleTitle,
                    Functions = functionDtos
                });
            }

            _logger.LogInformation("成功組裝依賴階層式權限樹，共處理模組數: {Count}", moduleDtos.Count);
            return moduleDtos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "組裝權限樹狀結構時發生未預期例外。");
            throw;
        }
    }


    public async Task<RolePermissionMatrixDto?> GetRolePermissionsAsync(string roleId, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return null;
    }

    public async Task<(bool Succeeded, string Message)> UpdateRolePermissionsAsync(
        UpdateRolePermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return (true, string.Empty);
    }
}