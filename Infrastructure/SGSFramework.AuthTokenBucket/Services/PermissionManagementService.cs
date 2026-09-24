#nullable enable

// ==========================================
// 檔案路徑: Application/SGSFramework.AuthTokenBucket/Services/PermissionManagementService.cs
// 架構層級: Application Layer (Service Implementation)
// ==========================================

namespace SGSFramework.AuthTokenBucket.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.DTOs.PermissionGrants;
using SGSFramework.AuthTokenBucket.DTOs.PermissionTree;
using SGSFramework.AuthTokenBucket.DTOs.RolePermissions;
using SGSFramework.AuthTokenBucket.Repositories;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using SGSFramework.Core.Abstractions.Permissions.Identities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class PermissionManagementService<TDbContext> : IPermissionManagementService
    where TDbContext : DbContext, ITokenDbContext
{
    private readonly TDbContext _dbContext;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IPermissionBitmaskService _bitmaskService;
    private readonly ILogger<PermissionManagementService<TDbContext>> _logger;

    public PermissionManagementService(
        TDbContext dbContext,
        IRolePermissionRepository rolePermissionRepository,
        IPermissionBitmaskService bitmaskService,
        ILogger<PermissionManagementService<TDbContext>> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _rolePermissionRepository = rolePermissionRepository ?? throw new ArgumentNullException(nameof(rolePermissionRepository));
        _bitmaskService = bitmaskService ?? throw new ArgumentNullException(nameof(bitmaskService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<bool> HasPermissionAsync(string userId, string permissionCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

        return await Task.FromResult(true).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task GrantPermissionToRoleAsync(string roleId, string permissionCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task RevokePermissionFromRoleAsync(string roleId, string permissionCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionCode);

        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<PermissionMetadataDto>> SyncPermissionMetadataAsync(CancellationToken cancellationToken = default)
    {
        var metadataList = await _dbContext.Set<PermissionMetadata>()
            .AsNoTracking()
            .Select(m => new PermissionMetadataDto(
                m.Id,
                m.ModuleName ?? string.Empty,
                m.ModuleTitle ?? string.Empty,
                m.ControllerName ?? string.Empty,
                m.ControllerTitle ?? string.Empty,
                m.ActionName ?? string.Empty,
                m.ActionTitle ?? string.Empty,
                m.PermissionKey ?? string.Empty,
                m.PermissionTitle ?? string.Empty,
                m.Description ?? string.Empty,
                m.BitPosition
            ))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return metadataList.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<List<PermissionModuleDto>> GetPermissionTreeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var metadataList = await _dbContext.Set<PermissionMetadata>()
                .AsNoTracking()
                .OrderBy(m => m.ModuleTitle)
                .ThenBy(m => m.ControllerTitle)
                .ThenBy(m => m.PermissionKey)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (metadataList.Count == 0)
            {
                return new List<PermissionModuleDto>();
            }

            var result = new List<PermissionModuleDto>();
            var moduleGroups = metadataList.GroupBy(m => new { m.ModuleName, m.ModuleTitle });

            foreach (var moduleGroup in moduleGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var moduleDto = new PermissionModuleDto
                {
                    ModuleName = moduleGroup.Key.ModuleName ?? string.Empty,
                    ModuleTitle = moduleGroup.Key.ModuleTitle ?? string.Empty,
                    Description = moduleGroup.FirstOrDefault()?.Description,
                    Functions = new List<PermissionFunctionDto>()
                };

                var controllerGroups = moduleGroup.GroupBy(c => new { c.ControllerName, c.ControllerTitle });

                foreach (var controllerGroup in controllerGroups)
                {
                    var functionDto = new PermissionFunctionDto
                    {
                        FunctionName = controllerGroup.Key.ControllerName ?? string.Empty,
                        FunctionTitle = controllerGroup.Key.ControllerTitle ?? string.Empty,
                        Description = controllerGroup.FirstOrDefault()?.Description,
                        ActionPermissions = new List<PermissionActionDto>()
                    };

                    var readEntity = controllerGroup.FirstOrDefault(a =>
                        (!string.IsNullOrEmpty(a.PermissionKey) && a.PermissionKey.EndsWith(".READ", StringComparison.OrdinalIgnoreCase)) ||
                        string.Equals(a.PermissionTitle, controllerGroup.Key.ControllerTitle, StringComparison.OrdinalIgnoreCase));

                    if (readEntity != null)
                    {
                        functionDto.ReadPermission = new PermissionActionDto
                        {
                            PermissionKey = readEntity.PermissionKey ?? string.Empty,
                            PermissionTitle = readEntity.PermissionTitle,
                            ActionName = readEntity.ActionName,
                            ActionTitle = readEntity.ActionTitle,
                            Description = readEntity.Description,
                            BitPosition = readEntity.BitPosition
                        };
                    }

                    foreach (var actionEntity in controllerGroup)
                    {
                        if (readEntity != null && actionEntity.PermissionKey == readEntity.PermissionKey)
                        {
                            continue;
                        }

                        functionDto.ActionPermissions.Add(new PermissionActionDto
                        {
                            PermissionKey = actionEntity.PermissionKey ?? string.Empty,
                            PermissionTitle = actionEntity.PermissionTitle,
                            ActionName = actionEntity.ActionName,
                            ActionTitle = !string.IsNullOrWhiteSpace(actionEntity.ActionTitle)
                                ? actionEntity.ActionTitle
                                : actionEntity.PermissionTitle,
                            Description = actionEntity.Description,
                            BitPosition = actionEntity.BitPosition
                        });
                    }

                    moduleDto.Functions.Add(functionDto);
                }

                result.Add(moduleDto);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PermissionManagementService] 建立權限樹狀圖時發生錯誤");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<RolePermissionMatrixDto?> GetRoleGlobalPermissionsAsync(string roleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleId);

        try
        {
           
            var rolePermissions = await _dbContext.Set<RoleGlobalPermission>()
                .AsNoTracking()
                .Where(r => r.RoleId == roleId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (rolePermissions.Count == 0)
            {
                return new RolePermissionMatrixDto
                {
                    RoleId = roleId,
                    GrantedPermissionKeys = new List<string>()
                };
            }

            Dictionary<string, long> dbBitmaskMap;

            dbBitmaskMap = rolePermissions
                    .Select(x => new { x.PermissionKey, x.Bitmask }).ToDictionary(
                    x => x.PermissionKey,
                    x => x.Bitmask,
                    StringComparer.OrdinalIgnoreCase);

            // 解碼權限
            var decodedDirectPermissions = await _bitmaskService.DecodeBitmaskToPermissionsAsync(dbBitmaskMap, cancellationToken).ConfigureAwait(false);

            return new RolePermissionMatrixDto
            {
                RoleId = roleId,
                GrantedPermissionKeys = decodedDirectPermissions
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[PermissionManagementService] 查詢角色 [{RoleId}] 權限作業已取消。", roleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PermissionManagementService] 查詢角色 [{RoleId}] 權限矩陣時發生未預期異常。", roleId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<(bool Succeeded, string Message)> UpdateRolePermissionsAsync(UpdateRoleGlobalPermissionsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RoleId))
        {
            _logger.LogWarning("[PermissionManagementService] 更新失敗：傳入的 RoleId 為空。");
            return (false, "角色識別碼 (RoleId) 不得為空。");
        }

        try
        {
            var targetPermissions = request.PermissionKeys?
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

                //  Bitmask 計算服務
                Dictionary<string, long> moduleBitmaskDict = await _bitmaskService
                    .CalculateModuleBitmasksAsync(targetPermissions, cancellationToken)
                    .ConfigureAwait(false);

            bool success = await _rolePermissionRepository
                .SaveRoleGlobalPermissionsAsync(request.RoleId, moduleBitmaskDict, cancellationToken)
                .ConfigureAwait(false);

            if (!success)
            {
                _logger.LogWarning("[PermissionManagementService] 角色全域權限寫入資料庫失敗。RoleId: {RoleId}", request.RoleId);
                return (false, "寫入角色全域權限資料時發生失敗。");
            }

            _logger.LogInformation(
                "[PermissionManagementService] 成功更新角色 [{RoleId}] 全域權限，共影響 [{Count}] 個模組。",
                request.RoleId,
                moduleBitmaskDict.Count);

            return (true, $"角色全域權限更新成功，共更新 {moduleBitmaskDict.Count} 個模組區段。");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[PermissionManagementService] 更新角色 [{RoleId}] 全域權限作業已取消。", request.RoleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PermissionManagementService] 更新角色 [{RoleId}] 全域權限時發生未預期異常。", request.RoleId);
            throw;
        }
    }
}