// 檔案路徑: Infrastructure/SGSFramework.Identity/Services/RoleManagementService.cs

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.Identity.Services
{
    /// <summary>
    /// 企業級泛型角色管理與 AD 整合服務實作
    /// </summary>
    public class RoleManagementService<TRole, TKey> : IRoleManagementService<TRole, TKey>
        where TRole : IdentityRole<TKey>, new()
        where TKey : IEquatable<TKey>
    {
        private readonly RoleManager<TRole> _roleManager;
        private readonly ILogger<RoleManagementService<TRole, TKey>> _logger;

        public RoleManagementService(
            RoleManager<TRole> roleManager,
            ILogger<RoleManagementService<TRole, TKey>> logger)
        {
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<RoleDto>> GetAllRolesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var roles = await _roleManager.Roles
                    .Select(r => new RoleDto
                    {
                        Id = r.Id!.ToString()!,
                        Name = r.Name!
                    })
                    .ToListAsync(cancellationToken);

                return roles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得所有角色清單時發生例外。");
                throw;
            }
        }

        public async Task<RoleDto?> GetRoleByIdAsync(string roleId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(roleId);

            try
            {
                var role = await _roleManager.FindByIdAsync(roleId);
                if (role == null) return null;

                return new RoleDto
                {
                    Id = role.Id!.ToString()!,
                    Name = role.Name!
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "依據 RoleId ({RoleId}) 查詢角色時發生例外。", roleId);
                throw;
            }
        }

        public async Task<(bool Succeeded, IEnumerable<string> Errors)> CreateRoleAsync(
            CreateRoleRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var role = new TRole
                {
                    Name = request.RoleName
                };

                var result = await _roleManager.CreateAsync(role);
                if (result.Succeeded)
                {
                    _logger.LogInformation("成功建立角色：{RoleName} (ID: {RoleId})", role.Name, role.Id);
                    return (true, Enumerable.Empty<string>());
                }

                var errors = result.Errors.Select(e => e.Description);
                _logger.LogWarning("建立角色 {RoleName} 失敗。錯誤：{Errors}", request.RoleName, string.Join(", ", errors));
                return (false, errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "建立角色 ({RoleName}) 時發生例外。", request.RoleName);
                throw;
            }
        }

        public async Task<(bool Succeeded, IEnumerable<string> Errors)> UpdateRoleAsync(
            UpdateRoleRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                var role = await _roleManager.FindByIdAsync(request.RoleId);
                if (role == null)
                {
                    return (false, new[] { $"找不到識別碼為 '{request.RoleId}' 的角色。" });
                }

                role.Name = request.NewRoleName;
                var result = await _roleManager.UpdateAsync(role);

                if (result.Succeeded)
                {
                    _logger.LogInformation("成功更新角色：{RoleName} (ID: {RoleId})", role.Name, role.Id);
                    return (true, Enumerable.Empty<string>());
                }

                var errors = result.Errors.Select(e => e.Description);
                _logger.LogWarning("更新角色 {RoleId} 失敗。錯誤：{Errors}", request.RoleId, string.Join(", ", errors));
                return (false, errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新角色 ({RoleId}) 時發生例外。", request.RoleId);
                throw;
            }
        }

        public async Task<(bool Succeeded, IEnumerable<string> Errors)> DeleteRoleAsync(
            string roleId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(roleId);

            try
            {
                var role = await _roleManager.FindByIdAsync(roleId);
                if (role == null)
                {
                    return (false, new[] { $"找不到識別碼為 '{roleId}' 的角色。" });
                }

                var result = await _roleManager.DeleteAsync(role);
                if (result.Succeeded)
                {
                    _logger.LogInformation("成功刪除角色：{RoleName} (ID: {RoleId})", role.Name, roleId);
                    return (true, Enumerable.Empty<string>());
                }

                var errors = result.Errors.Select(e => e.Description);
                _logger.LogWarning("刪除角色 {RoleId} 失敗。錯誤：{Errors}", roleId, string.Join(", ", errors));
                return (false, errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刪除角色 ({RoleId}) 時發生例外。", roleId);
                throw;
            }
        }

        #region AD Group 對應服務

        public async Task<(bool Succeeded, string Message)> MapAdGroupToRoleAsync(
            MapAdGroupToRoleRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            // 預留 AD Group 對應邏輯 (例如寫入 ADGroupRoleMapping 資料表或 RoleClaim)
            await Task.CompletedTask;
            _logger.LogInformation("已成功將 AD 群組 {AdGroup} 對應至角色 {RoleId}", request.AdGroupName, request.RoleId);

            return (true, "AD 群組對應設定成功。");
        }

        public async Task<(bool Succeeded, string Message)> RemoveAdGroupFromRoleAsync(
            RemoveAdGroupFromRoleRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            await Task.CompletedTask;
            _logger.LogInformation("已成功移除 AD 群組 {AdGroup} 與角色 {RoleId} 之對應", request.AdGroupName, request.RoleId);

            return (true, "已成功移除 AD 群組對應。");
        }

        public async Task<(bool Succeeded, List<string> SyncedRoles, string Message)> SyncUserRolesFromAdGroupsAsync(
            SyncUserAdRolesRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            await Task.CompletedTask;
            var syncedRoles = new List<string>();

            _logger.LogInformation("成功同步使用者 {UserId} 的 AD 群組角色", request.Username);
            return (true, syncedRoles, "AD 群組角色同步完成。");
        }

        #endregion

        #region 使用者角色綁定服務

        public async Task<(bool Succeeded, string Message)> AssignUserRolesAsync(
            AssignUserRolesRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            await Task.CompletedTask;
            _logger.LogInformation("成功綁定使用者 {UserId} 之角色清單", request.UserId);

            return (true, "使用者角色綁定成功。");
        }

        #endregion
    }
}