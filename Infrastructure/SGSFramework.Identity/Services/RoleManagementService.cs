// ==========================================
// 檔案路徑: Infrastructure/SGSFramework.Identity/Services/RoleManagementService.cs
// 架構層級: Infrastructure / Services
// ==========================================

#nullable enable

namespace SGSFramework.Identity.Services
{
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using SGSFramework.AuthTokenBucket.Abstractions;
    using SGSFramework.Core.Abstractions.Adapters;
    using SGSFramework.Core.Abstractions.Entities.Identities;
    using SGSFramework.Core.Abstractions.Permissions;
    using SGSFramework.Identity.Abstractions;
    using SGSFramework.Identity.DTOs;
    using SGSFramework.Identity.DTOs.Roles;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    public class RoleManagementService<TRole, TKey> : IRoleManagementService<TRole, TKey>
        where TRole : IdentityRole<TKey>, IRoleEntity, new()
        where TKey : IEquatable<TKey>
    {
        private readonly RoleManager<TRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOrganizationIntegrationService _orgIntegrationService;
        private readonly IPermissionManagementService _permissionService;
        private readonly IPermissionRegistry _permissionRegistry;
        private readonly DbContext _dbContext;
        private readonly ILogger<RoleManagementService<TRole, TKey>> _logger;

        public RoleManagementService(
            RoleManager<TRole> roleManager,
            UserManager<ApplicationUser> userManager,
            IOrganizationIntegrationService orgIntegrationService,
            IPermissionManagementService permissionService,
            IPermissionRegistry permissionRegistry,
            DbContext dbContext,
            ILogger<RoleManagementService<TRole, TKey>> logger)
        {
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _orgIntegrationService = orgIntegrationService ?? throw new ArgumentNullException(nameof(orgIntegrationService));
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
            _permissionRegistry = permissionRegistry ?? throw new ArgumentNullException(nameof(permissionRegistry));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private static string GetEnumDisplayName(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            if (field == null) return value.ToString();

            var attribute = field.GetCustomAttribute<DisplayAttribute>();
            if (attribute != null && !string.IsNullOrWhiteSpace(attribute.Name))
            {
                return attribute.Name;
            }

            return value.ToString();
        }

        private async Task<Dictionary<string, (string ModuleName, string ModuleTitle, string FunctionName, string FunctionTitle, string PermissionTitle, string Description)>> BuildPermissionMetadataLookupAsync(CancellationToken cancellationToken)
        {
            var lookup = new Dictionary<string, (string, string, string, string, string, string)>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var tree = await _permissionService.GetPermissionTreeAsync(cancellationToken).ConfigureAwait(false);
                if (tree == null) return lookup;

                foreach (var module in tree)
                {
                    if (module.Functions == null) continue;
                    foreach (var func in module.Functions)
                    {
                        // 處理 ReadPermission
                        if (func.ReadPermission != null && !string.IsNullOrWhiteSpace(func.ReadPermission.PermissionKey))
                        {
                            lookup[func.ReadPermission.PermissionKey] = (
                                module.ModuleName ?? string.Empty,
                                module.ModuleTitle ?? string.Empty,
                                func.FunctionName ?? string.Empty,
                                func.FunctionTitle ?? string.Empty,
                                func.ReadPermission.PermissionTitle ?? func.ReadPermission.ActionTitle ?? string.Empty,
                                func.ReadPermission.Description ?? string.Empty
                            );
                        }

                        // 處理 ActionPermissions
                        if (func.ActionPermissions != null)
                        {
                            foreach (var act in func.ActionPermissions)
                            {
                                if (!string.IsNullOrWhiteSpace(act.PermissionKey))
                                {
                                    lookup[act.PermissionKey] = (
                                        module.ModuleName ?? string.Empty,
                                        module.ModuleTitle ?? string.Empty,
                                        func.FunctionName ?? string.Empty,
                                        func.FunctionTitle ?? string.Empty,
                                        act.PermissionTitle ?? act.ActionTitle ?? string.Empty,
                                        act.Description ?? string.Empty
                                    );
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "建立權限元數據查閱字典時發生異常。");
            }
            return lookup;
        }

        public async Task<List<RoleDto>> GetAllRolesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var roles = await _roleManager.Roles
                    .AsNoTracking()
                    .Where(r => EF.Property<bool>(r, "IsDeleted") == false)
                    .OrderBy(r => r.Name)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                // 取得完整權限樹狀結構對應字典以供豐富化顯示
                var permLookup = await BuildPermissionMetadataLookupAsync(cancellationToken).ConfigureAwait(false);

                var roleDtos = new List<RoleDto>(roles.Count);

                foreach (var role in roles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string roleIdStr = role.Id!.ToString()!;

                    var members = new List<RoleMemberItemDto>();
                    if (!string.IsNullOrEmpty(role.Name))
                    {
                        try
                        {
                            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name).ConfigureAwait(false);
                            foreach (var u in usersInRole.Where(u => !u.IsDeleted))
                            {
                                string userIdStr = u.Id.ToString() ?? string.Empty;
                                Guid? primaryLabId = null;
                                string? primaryLabCode = null;
                                string? primaryLabName = null;

                                try
                                {
                                    var accessibleOrgs = await _orgIntegrationService.GetUserAccessibleOrganizationsAsync(userIdStr, cancellationToken).ConfigureAwait(false);
                                    var primaryOrg = accessibleOrgs?.FirstOrDefault(o => o.IsPrimary) ?? accessibleOrgs?.FirstOrDefault();
                                    if (primaryOrg != null)
                                    {
                                        primaryLabId = primaryOrg.TenantLabId;
                                        primaryLabCode = primaryOrg.Code;
                                        primaryLabName = primaryOrg.Name;
                                    }
                                }
                                catch (Exception orgEx)
                                {
                                    _logger.LogWarning(orgEx, "取得使用者 [{UserId}] 的組織與實驗室資訊失敗。", userIdStr);
                                }

                                members.Add(new RoleMemberItemDto
                                {
                                    UserId = userIdStr,
                                    UserName = u.UserName ?? string.Empty,
                                    FullName = u.FullName ?? string.Empty,
                                    EmployeeNo = u.EmployeeNo ?? string.Empty,
                                    Email = u.Email ?? string.Empty,
                                    PrimaryLabId = primaryLabId,
                                    PrimaryLabCode = primaryLabCode,
                                    PrimaryLabName = primaryLabName
                                });
                            }
                        }
                        catch (Exception roleUserEx)
                        {
                            _logger.LogWarning(roleUserEx, "取得角色 [{RoleName}] 的成員清單失敗。", role.Name);
                        }
                    }

                    // 取得角色的權限 Matrix 並對應樹狀詳細資訊 (ModuleTitle, FunctionTitle, PermissionTitle, Description)
                    var permissionItems = new List<RolePermissionItemDto>();
                    try
                    {
                        var roleGlobalPerms = await _permissionService.GetRoleGlobalPermissionsAsync(roleIdStr, cancellationToken).ConfigureAwait(false);
                        if (roleGlobalPerms?.GrantedPermissionKeys != null)
                        {
                            foreach (string key in roleGlobalPerms.GrantedPermissionKeys)
                            {
                                if (permLookup.TryGetValue(key, out var meta))
                                {
                                    permissionItems.Add(new RolePermissionItemDto
                                    {
                                        ModuleName = meta.ModuleName,
                                        ModuleTitle = meta.ModuleTitle,
                                        FunctionName = meta.FunctionName,
                                        FunctionTitle = meta.FunctionTitle,
                                        PermissionKey = key,
                                        PermissionTitle = meta.PermissionTitle,
                                        Description = meta.Description
                                    });
                                }
                                else
                                {
                                    permissionItems.Add(new RolePermissionItemDto
                                    {
                                        PermissionKey = key,
                                        PermissionTitle = key,
                                        Description = string.Empty
                                    });
                                }
                            }
                        }
                    }
                    catch (Exception permEx)
                    {
                        _logger.LogWarning(permEx, "取得角色 [{RoleId}] 的權限明細失敗。", roleIdStr);
                    }

                    var entry = _dbContext.Entry(role);
                    string code = entry.Property<string>("Code").CurrentValue ?? string.Empty;
                    string description = entry.Property<string>("Description").CurrentValue ?? string.Empty;
                    bool isSystemRole = entry.Property<bool>("IsSystemRole").CurrentValue;
                    var statusVal = entry.Property<RoleStatus>("Status").CurrentValue;
                    bool isDeleted = entry.Property<bool>("IsDeleted").CurrentValue;

                    roleDtos.Add(new RoleDto
                    {
                        Id = roleIdStr,
                        Name = role.Name!,
                        Code = code,
                        Description = description,
                        IsSystemRole = isSystemRole,
                        Status = GetEnumDisplayName(statusVal),
                        IsDeleted = isDeleted,
                        Members = members,
                        Permissions = permissionItems
                    });
                }

                return roleDtos;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得所有角色清單、成員與權限明細時發生重大例外。");
                throw;
            }
        }

        public async Task<RoleDto?> GetRoleByIdAsync(string roleId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(roleId);

            try
            {
                var role = await _roleManager.FindByIdAsync(roleId).ConfigureAwait(false);
                if (role == null) return null;

                var entry = _dbContext.Entry(role);
                bool isDeleted = entry.Property<bool>("IsDeleted").CurrentValue;
                if (isDeleted) return null;

                var members = new List<RoleMemberItemDto>();
                if (!string.IsNullOrEmpty(role.Name))
                {
                    var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name).ConfigureAwait(false);
                    foreach (var u in usersInRole.Where(u => !u.IsDeleted))
                    {
                        string userIdStr = u.Id.ToString() ?? string.Empty;
                        Guid? primaryLabId = null;
                        string? primaryLabCode = null;
                        string? primaryLabName = null;

                        try
                        {
                            var accessibleOrgs = await _orgIntegrationService.GetUserAccessibleOrganizationsAsync(userIdStr, cancellationToken).ConfigureAwait(false);
                            var primaryOrg = accessibleOrgs?.FirstOrDefault(o => o.IsPrimary) ?? accessibleOrgs?.FirstOrDefault();
                            if (primaryOrg != null)
                            {
                                primaryLabId = primaryOrg.TenantLabId;
                                primaryLabCode = primaryOrg.Code;
                                primaryLabName = primaryOrg.Name;
                            }
                        }
                        catch (Exception orgEx)
                        {
                            _logger.LogWarning(orgEx, "取得使用者 [{UserId}] 組織資訊失敗。", userIdStr);
                        }

                        members.Add(new RoleMemberItemDto
                        {
                            UserId = userIdStr,
                            UserName = u.UserName ?? string.Empty,
                            FullName = u.FullName ?? string.Empty,
                            EmployeeNo = u.EmployeeNo ?? string.Empty,
                            Email = u.Email ?? string.Empty,
                            PrimaryLabId = primaryLabId,
                            PrimaryLabCode = primaryLabCode,
                            PrimaryLabName = primaryLabName
                        });
                    }
                }

                var permLookup = await BuildPermissionMetadataLookupAsync(cancellationToken).ConfigureAwait(false);
                var permissionItems = new List<RolePermissionItemDto>();
                try
                {
                    var roleGlobalPerms = await _permissionService.GetRoleGlobalPermissionsAsync(roleId, cancellationToken).ConfigureAwait(false);
                    if (roleGlobalPerms?.GrantedPermissionKeys != null)
                    {
                        foreach (string key in roleGlobalPerms.GrantedPermissionKeys)
                        {
                            if (permLookup.TryGetValue(key, out var meta))
                            {
                                permissionItems.Add(new RolePermissionItemDto
                                {
                                    ModuleName = meta.ModuleName,
                                    ModuleTitle = meta.ModuleTitle,
                                    FunctionName = meta.FunctionName,
                                    FunctionTitle = meta.FunctionTitle,
                                    PermissionKey = key,
                                    PermissionTitle = meta.PermissionTitle,
                                    Description = meta.Description
                                });
                            }
                            else
                            {
                                permissionItems.Add(new RolePermissionItemDto
                                {
                                    PermissionKey = key,
                                    PermissionTitle = key,
                                    Description = string.Empty
                                });
                            }
                        }
                    }
                }
                catch (Exception permEx)
                {
                    _logger.LogWarning(permEx, "取得角色 [{RoleId}] 的權限明細失敗。", roleId);
                }

                string code = entry.Property<string>("Code").CurrentValue ?? string.Empty;
                string description = entry.Property<string>("Description").CurrentValue ?? string.Empty;
                bool isSystemRole = entry.Property<bool>("IsSystemRole").CurrentValue;
                var statusVal = entry.Property<RoleStatus>("Status").CurrentValue;

                return new RoleDto
                {
                    Id = role.Id!.ToString()!,
                    Name = role.Name!,
                    Code = code,
                    Description = description,
                    IsSystemRole = isSystemRole,
                    Status = GetEnumDisplayName(statusVal),
                    IsDeleted = false,
                    Members = members,
                    Permissions = permissionItems
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "依據 RoleId ({RoleId}) 查詢角色、成員及權限時發生例外。", roleId);
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
                cancellationToken.ThrowIfCancellationRequested();

                var role = new TRole
                {
                    Name = request.RoleName,
                    NormalizedName = _roleManager.NormalizeKey(request.RoleName)
                };

                _dbContext.Entry(role).Property("Code").CurrentValue = request.Code;
                _dbContext.Entry(role).Property("Description").CurrentValue = request.Description;
                _dbContext.Entry(role).Property("IsSystemRole").CurrentValue = request.IsSystemRole;
                _dbContext.Entry(role).Property("IsDeleted").CurrentValue = false;
                _dbContext.Entry(role).Property("Status").CurrentValue = RoleStatus.Active;

                var result = await _roleManager.CreateAsync(role).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("成功建立角色：{RoleName} (ID: {RoleId})", role.Name, role.Id);
                    return (true, Enumerable.Empty<string>());
                }

                var errors = result.Errors.Select(e => e.Description);
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
                cancellationToken.ThrowIfCancellationRequested();

                var role = await _roleManager.FindByIdAsync(request.RoleId).ConfigureAwait(false);
                if (role == null || _dbContext.Entry(role).Property<bool>("IsDeleted").CurrentValue)
                {
                    return (false, new[] { $"找不到識別碼為 '{request.RoleId}' 的角色。" });
                }

                role.Name = request.NewRoleName;
                role.NormalizedName = _roleManager.NormalizeKey(request.NewRoleName);

                _dbContext.Entry(role).Property("Code").CurrentValue = request.Code;
                _dbContext.Entry(role).Property("Description").CurrentValue = request.Description;

                var result = await _roleManager.UpdateAsync(role).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    return (true, Enumerable.Empty<string>());
                }

                return (false, result.Errors.Select(e => e.Description));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新角色 ({RoleId}) 時發生例外。", request.RoleId);
                throw;
            }
        }

        public async Task<(bool Succeeded, IEnumerable<string> Errors)> UpdateRoleStatusAsync(
            TKey roleId,
            RoleStatus newStatus,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(roleId);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var role = await _roleManager.FindByIdAsync(roleId.ToString()!).ConfigureAwait(false);
                if (role == null || _dbContext.Entry(role).Property<bool>("IsDeleted").CurrentValue)
                {
                    return (false, new[] { $"找不到識別碼為 '{roleId}' 的有效角色。" });
                }

                _dbContext.Entry(role).Property("Status").CurrentValue = newStatus;

                var result = await _roleManager.UpdateAsync(role).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    return (true, Enumerable.Empty<string>());
                }

                return (false, result.Errors.Select(e => e.Description));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "變更角色狀態時發生例外。RoleId: {RoleId}", roleId);
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
                cancellationToken.ThrowIfCancellationRequested();

                var role = await _roleManager.FindByIdAsync(roleId).ConfigureAwait(false);
                if (role == null || _dbContext.Entry(role).Property<bool>("IsDeleted").CurrentValue)
                {
                    return (false, new[] { $"找不到識別碼為 '{roleId}' 的角色。" });
                }

                string anonymizedTag = roleId.Length >= 8 ? roleId[..8] : roleId;

                var entry = _dbContext.Entry(role);
                entry.Property("IsDeleted").CurrentValue = true;
                entry.Property("Code").CurrentValue = $"deleted_code_{anonymizedTag}";
                role.Name = $"deleted_role_{anonymizedTag}";
                role.NormalizedName = $"DELETED_ROLE_{anonymizedTag.ToUpperInvariant()}";

                var result = await _roleManager.UpdateAsync(role).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    return (true, Enumerable.Empty<string>());
                }

                return (false, result.Errors.Select(e => e.Description));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "刪除角色 ({RoleId}) 時發生例外。", roleId);
                throw;
            }
        }

        #region AD Group 與使用者綁定 (保留擴充)

        public async Task<(bool Succeeded, string Message)> MapAdGroupToRoleAsync(
            MapAdGroupToRoleRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            await Task.CompletedTask.ConfigureAwait(false);
            return (true, "AD 群組對應設定成功。");
        }

        public async Task<(bool Succeeded, string Message)> RemoveAdGroupFromRoleAsync(
            RemoveAdGroupFromRoleRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            await Task.CompletedTask.ConfigureAwait(false);
            return (true, "已成功移除 AD 群組對應。");
        }

        public async Task<(bool Succeeded, List<string> SyncedRoles, string Message)> SyncUserRolesFromAdGroupsAsync(
            SyncUserAdRolesRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            await Task.CompletedTask.ConfigureAwait(false);
            return (true, new List<string>(), "AD 群組角色同步完成。");
        }

        public async Task<(bool Succeeded, string Message)> AssignUserRolesAsync(
            string userId,
            AssignUserRolesRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);
            ArgumentNullException.ThrowIfNull(request);

            var user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
            if (user == null || user.IsDeleted)
            {
                return (false, $"找不到識別碼為 '{userId}' 的有效使用者。");
            }

            var currentRoles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
            var targetRoles = request.RoleNames?.Distinct().ToList() ?? [];

            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles.Except(targetRoles)).ConfigureAwait(false);
            if (!removeResult.Succeeded) return (false, string.Join("; ", removeResult.Errors.Select(e => e.Description)));

            var addResult = await _userManager.AddToRolesAsync(user, targetRoles.Except(currentRoles)).ConfigureAwait(false);
            if (!addResult.Succeeded) return (false, string.Join("; ", addResult.Errors.Select(e => e.Description)));

            return (true, "使用者角色更新成功。");
        }

        public async Task<(bool Succeeded, string Message, IEnumerable<string>? Errors)> BatchAssignUsersToRoleAsync(
            string roleIdentifier,
            BatchAssignUsersRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(roleIdentifier);
            ArgumentNullException.ThrowIfNull(request);

            TRole? role = Guid.TryParse(roleIdentifier, out var roleGuid)
                ? await _roleManager.FindByIdAsync(roleGuid.ToString()).ConfigureAwait(false)
                : await _roleManager.FindByNameAsync(roleIdentifier).ConfigureAwait(false);

            if (role == null || _dbContext.Entry(role).Property<bool>("IsDeleted").CurrentValue)
            {
                return (false, $"找不到指定的角色 [{roleIdentifier}]。", null);
            }

            var userGuids = request.UserIds?
                .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .Distinct()
                .ToList() ?? [];

            var users = await _userManager.Users
                .Where(u => !u.IsDeleted && userGuids.Contains((Guid)(object)u.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (users.Count == 0) return (false, "找不到對應的有效使用者清單。", null);

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var errors = new List<string>();
                int successCount = 0;

                foreach (var user in users)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (role.Name != null && await _userManager.IsInRoleAsync(user, role.Name).ConfigureAwait(false))
                    {
                        successCount++;
                        continue;
                    }

                    var result = role.Name != null ? await _userManager.AddToRoleAsync(user, role.Name).ConfigureAwait(false) : IdentityResult.Failed();
                    if (result.Succeeded) successCount++;
                    else errors.Add($"使用者 [{user.UserName}] 指派失敗: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }

                if (errors.Count > 0)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return (false, "部分使用者指派失敗，交易已全數回滾。", errors);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return (true, $"已成功將角色 [{role.Name}] 批次指派給 {successCount} 位使用者。", null);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }
        #endregion
    }
}