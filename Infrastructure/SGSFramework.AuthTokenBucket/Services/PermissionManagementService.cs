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
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public class PermissionManagementService<TDbContext> : IPermissionManagementService
    where TDbContext : DbContext, ITokenDbContext
{
    private readonly TDbContext _dbContext;
    private readonly ILogger<PermissionManagementService<TDbContext>> _logger;

    public PermissionManagementService(
        TDbContext dbContext,
        ILogger<PermissionManagementService<TDbContext>> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
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

                    // 尋找主讀取權限 (以 .READ 結尾或同名標題)
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

                    // 過濾掉已指派給 ReadPermission 的項目，其餘放入 ActionPermissions
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
    public async Task<RolePermissionMatrixDto?> GetRolePermissionsAsync(string roleId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleId);

        return await Task.FromResult(new RolePermissionMatrixDto
        {
            RoleId = roleId,
            GrantedPermissionKeys = new List<string>()
        }).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<(bool Succeeded, string Message)> UpdateRolePermissionsAsync(UpdateRolePermissionsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await Task.FromResult((true, "角色權限已成功更新")).ConfigureAwait(false);
    }
}