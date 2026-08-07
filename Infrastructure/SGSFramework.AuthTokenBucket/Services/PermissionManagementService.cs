using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.DTOs;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Permissions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Services
{
    public class PermissionManagementService<TDbContext> : IPermissionManagementService
            where TDbContext : DbContext, ITokenDbContext
    {
        private readonly TDbContext _dbContext;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<PermissionManagementService<TDbContext>> _logger;

        public PermissionManagementService(
            TDbContext dbContext,
            RoleManager<IdentityRole> roleManager,
            ILogger<PermissionManagementService<TDbContext>> logger)
        {
            _dbContext = dbContext;
            _roleManager = roleManager;
            _logger = logger;
        }

        public async Task<List<PermissionModuleDto>> GetPermissionTreeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var permissions = await _dbContext.Set<Permission>()
                    .AsNoTracking()
                    .OrderBy(p => p.ModuleName)
                    .ThenBy(p => p.ControllerName)
                    .ThenBy(p => p.BitPosition)
                    .ToListAsync(cancellationToken);

                var tree = permissions
                    .GroupBy(p => string.IsNullOrWhiteSpace(p.ModuleName) ? "System" : p.ModuleName)
                    .Select(moduleGroup => new PermissionModuleDto
                    {
                        ModuleName = moduleGroup.Key,
                        Controllers = moduleGroup
                            .GroupBy(p => string.IsNullOrWhiteSpace(p.ControllerName) ? "General" : p.ControllerName)
                            .Select(ctrlGroup => new PermissionControllerDto
                            {
                                ControllerName = ctrlGroup.Key,
                                Permissions = ctrlGroup.Select(p => new PermissionItemDto
                                {
                                    Id = p.Id,
                                    PermissionKey = p.PermissionKey,
                                    BitPosition = p.BitPosition,
                                    ActionName = p.ActionName,
                                    Description = p.Description
                                }).ToList()
                            }).ToList()
                    }).ToList();

                return tree;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPermissionTreeAsync 發生未預期錯誤");
                throw;
            }
        }

        public async Task<RolePermissionMatrixDto?> GetRolePermissionsAsync(string roleId, CancellationToken cancellationToken = default)
        {
            try
            {
                var role = await _roleManager.FindByIdAsync(roleId);
                if (role == null)
                {
                    return null;
                }

                // 取得角色現有的 Claims (ClaimType 可自訂，預設以 PermissionKey 或 BitMask 為依據)
                var claims = await _roleManager.GetClaimsAsync(role);
                var grantedKeys = claims
                    .Where(c => c.Type == "Permission")
                    .Select(c => c.Value)
                    .ToList();

                var grantedPermissions = await _dbContext.Set<Permission>()
                    .AsNoTracking()
                    .Where(p => grantedKeys.Contains(p.PermissionKey))
                    .Select(p => p.BitPosition)
                    .ToListAsync(cancellationToken);

                return new RolePermissionMatrixDto
                {
                    RoleId = role.Id,
                    RoleName = role.Name ?? string.Empty,
                    GrantedPermissionKeys = grantedKeys,
                    GrantedBitPositions = grantedPermissions
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetRolePermissionsAsync 發生未預期錯誤，RoleId: {RoleId}", roleId);
                throw;
            }
        }

        public async Task<bool> UpdateRolePermissionsAsync(UpdateRolePermissionsRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var role = await _roleManager.FindByIdAsync(request.RoleId);
                if (role == null)
                {
                    return false;
                }

                var existingClaims = await _roleManager.GetClaimsAsync(role);
                var permissionClaims = existingClaims.Where(c => c.Type == "Permission").ToList();

                // 移除現有權限 Claims
                foreach (var claim in permissionClaims)
                {
                    await _roleManager.RemoveClaimAsync(role, claim);
                }

                // 寫入新權限 Claims
                foreach (var key in request.PermissionKeys.Distinct())
                {
                    await _roleManager.AddClaimAsync(role, new System.Security.Claims.Claim("Permission", key));
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "UpdateRolePermissionsAsync 寫入角色權限時發生錯誤，RoleId: {RoleId}", request.RoleId);
                throw;
            }
        }
    }
}
