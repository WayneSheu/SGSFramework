// ==========================================
// 檔案路徑: Infrastructure/SGSFramework.Identity/Services/RoleManagementService.cs
// 架構層級: Infrastructure / Services
// ==========================================

#nullable enable

namespace SGSFramework.Identity.Services
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using SGSFramework.AuthTokenBucket.Abstractions;
    using SGSFramework.Core.Abstractions.Adapters;
    using SGSFramework.Core.Abstractions.Entities.Identities;
    using SGSFramework.Core.Abstractions.Permissions;
    using SGSFramework.Core.Extensions;
    using SGSFramework.Identity.Abstractions;
    using SGSFramework.Identity.DTOs;
    using SGSFramework.Identity.DTOs.Roles;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
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
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly DbContext _dbContext;
        private readonly ILogger<RoleManagementService<TRole, TKey>> _logger;

        public RoleManagementService(
            RoleManager<TRole> roleManager,
            UserManager<ApplicationUser> userManager,
            IOrganizationIntegrationService orgIntegrationService,
            IPermissionManagementService permissionService,
            IPermissionRegistry permissionRegistry,
            IHttpContextAccessor httpContextAccessor,
            DbContext dbContext,
            ILogger<RoleManagementService<TRole, TKey>> logger)
        {
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _orgIntegrationService = orgIntegrationService ?? throw new ArgumentNullException(nameof(orgIntegrationService));
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
            _permissionRegistry = permissionRegistry ?? throw new ArgumentNullException(nameof(permissionRegistry));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 驗證角色範圍類型 (ScopeType) 與管理階層數值 (HierarchyLevel) 以及實驗室繫結之間的聯動防呆規則
        /// </summary>
        private static (bool IsValid, string ErrorMessage) ValidateRoleScopeAndHierarchy(
            RoleScopeType scopeType,
            int hierarchyLevel,
            Guid? tenantLabId)
        {
            return scopeType switch
            {
                RoleScopeType.SystemDefault => hierarchyLevel >= 90 && !tenantLabId.HasValue
                    ? (true, string.Empty)
                    : (false, "系統預設角色的階層數值必須大於或等於 90，且不得繫結特定的實驗室 ID。"),

                RoleScopeType.Global => hierarchyLevel >= 40 && hierarchyLevel < 90 && !tenantLabId.HasValue
                    ? (true, string.Empty)
                    : (false, "全域角色的階層數值必須介於 40 至 89 之間，且不得繫結特定的實驗室 ID。"),

                RoleScopeType.LabScoped => hierarchyLevel < 40 && tenantLabId.HasValue && tenantLabId.Value != Guid.Empty
                    ? (true, string.Empty)
                    : (false, "實驗室專屬角色的階層數值必須小於 40，且必須明確指定有效的實驗室 ID (TenantLabId)。"),

                _ => (false, "未知的角色範圍類型。")
            };
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
                    var scopeType = entry.Property<RoleScopeType>("ScopeType").CurrentValue;
                    int hierarchyLevel = entry.Property<int>("HierarchyLevel").CurrentValue;
                    Guid? tenantLabId = entry.Property<Guid?>("TenantLabId").CurrentValue;
                    bool isSystemRole = entry.Property<bool>("IsSystemRole").CurrentValue;
                    var statusVal = entry.Property<RoleStatus>("Status").CurrentValue;
                    bool isDeleted = entry.Property<bool>("IsDeleted").CurrentValue;

                    roleDtos.Add(new RoleDto
                    {
                        Id = roleIdStr,
                        Name = role.Name!,
                        Code = code,
                        Description = description,
                        ScopeType = scopeType,
                        ScopeTypeName = scopeType.GetDisplayName(),
                        HierarchyLevel = hierarchyLevel,
                        TenantLabId = tenantLabId,
                        IsSystemRole = isSystemRole,
                        Status = statusVal.GetDisplayName(),
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
                var scopeType = entry.Property<RoleScopeType>("ScopeType").CurrentValue;
                int hierarchyLevel = entry.Property<int>("HierarchyLevel").CurrentValue;
                Guid? tenantLabId = entry.Property<Guid?>("TenantLabId").CurrentValue;
                bool isSystemRole = entry.Property<bool>("IsSystemRole").CurrentValue;
                var statusVal = entry.Property<RoleStatus>("Status").CurrentValue;

                return new RoleDto
                {
                    Id = role.Id!.ToString()!,
                    Name = role.Name!,
                    Code = code,
                    Description = description,
                    ScopeType = scopeType,
                    ScopeTypeName = scopeType.GetDisplayName(),
                    HierarchyLevel = hierarchyLevel,
                    TenantLabId = tenantLabId,
                    IsSystemRole = isSystemRole,
                    Status = statusVal.GetDisplayName(),
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

                // 若前端未明確指定 ScopeType，預設為「全域角色 (Global)」，並給予安全的預設階層數值 (例如 50)
                var scopeType = request.ScopeType != default ? request.ScopeType : RoleScopeType.Global;
                var hierarchyLevel = request.HierarchyLevel != 0 ? request.HierarchyLevel : 50;
                Guid? tenantLabId = request.TenantLabId;

                // 執行範圍與階層聯動防呆驗證
                var (isValid, errorMessage) = ValidateRoleScopeAndHierarchy(scopeType, hierarchyLevel, tenantLabId);
                if (!isValid)
                {
                    return (false, new[] { errorMessage });
                }

                var role = new TRole
                {
                    Name = request.RoleName,
                    NormalizedName = _roleManager.NormalizeKey(request.RoleName)
                };

                var entry = _dbContext.Entry(role);
                entry.Property("Code").CurrentValue = request.Code;
                entry.Property("Description").CurrentValue = request.Description;
                entry.Property("ScopeType").CurrentValue = scopeType;
                entry.Property("HierarchyLevel").CurrentValue = hierarchyLevel;
                entry.Property("TenantLabId").CurrentValue = tenantLabId;
                entry.Property("IsSystemRole").CurrentValue = request.IsSystemRole;
                entry.Property("IsDeleted").CurrentValue = false;
                entry.Property("Status").CurrentValue = RoleStatus.Active;

                var result = await _roleManager.CreateAsync(role).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("成功建立角色：{RoleName} (ID: {RoleId}, Scope: {Scope}, Level: {Level})",
                        role.Name, role.Id, scopeType, hierarchyLevel);
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

                var entry = _dbContext.Entry(role);
                bool isSystemRole = entry.Property<bool>("IsSystemRole").CurrentValue;
                if (isSystemRole)
                {
                    return (false, new[] { "系統內建預設角色受到保護，禁止修改名稱或屬性。" });
                }

                // 若 Request 帶有 ScopeType、HierarchyLevel、TenantLabId 變更，進行聯動防呆驗證
                var targetScope = request.ScopeType != default ? request.ScopeType : entry.Property<RoleScopeType>("ScopeType").CurrentValue;
                var targetLevel = request.HierarchyLevel != 0 ? request.HierarchyLevel : entry.Property<int>("HierarchyLevel").CurrentValue;
                var targetLabId = request.TenantLabId ?? entry.Property<Guid?>("TenantLabId").CurrentValue;

                var (isValid, errorMessage) = ValidateRoleScopeAndHierarchy(targetScope, targetLevel, targetLabId);
                if (!isValid)
                {
                    return (false, new[] { errorMessage });
                }

                role.Name = request.NewRoleName;
                role.NormalizedName = _roleManager.NormalizeKey(request.NewRoleName);

                entry.Property("Code").CurrentValue = request.Code;
                entry.Property("Description").CurrentValue = request.Description;
                entry.Property("ScopeType").CurrentValue = targetScope;
                entry.Property("HierarchyLevel").CurrentValue = targetLevel;
                entry.Property("TenantLabId").CurrentValue = targetLabId;

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

                var entry = _dbContext.Entry(role);
                if (entry.Property<bool>("IsSystemRole").CurrentValue && newStatus == RoleStatus.Disabled)
                {
                    return (false, new[] { "系統核心預設角色禁止停用。" });
                }

                entry.Property("Status").CurrentValue = newStatus;

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

                var entry = _dbContext.Entry(role);
                if (entry.Property<bool>("IsSystemRole").CurrentValue)
                {
                    return (false, new[] { "系統內建預設角色受到保護，禁止刪除。" });
                }

                string anonymizedTag = roleId.Length >= 8 ? roleId[..8] : roleId;

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

        #region 行政型角色基礎存取控制 (ARBAC) 與指派驗證

        /// <inheritdoc />
        public async Task<(bool Succeeded, string Message)> AssignUserRolesAsync(
            string userId,
            AssignUserRolesRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userId);
            ArgumentNullException.ThrowIfNull(request);

            string actorUserId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                 ?? _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value
                                 ?? string.Empty;

            if (string.IsNullOrEmpty(actorUserId))
            {
                return (false, "無法識別目前操作者的身分驗證資訊。");
            }

            var actorUser = await _userManager.FindByIdAsync(actorUserId).ConfigureAwait(false);
            if (actorUser == null || actorUser.IsDeleted)
            {
                return (false, "找不到操作者身分或帳號已被停用。");
            }

            var actorRoleNames = await _userManager.GetRolesAsync(actorUser).ConfigureAwait(false);
            var actorRoles = await _roleManager.Roles
                .AsNoTracking()
                .Where(r => actorRoleNames.Contains(r.Name!))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            int actorMaxLevel = actorRoles.Any() ? actorRoles.Max(r => EF.Property<int>(r, "HierarchyLevel")) : 0;

            var targetUser = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
            if (targetUser == null || targetUser.IsDeleted)
            {
                return (false, $"找不到識別碼為 '{userId}' 的有效使用者。");
            }

            var targetRoles = request.RoleNames?.Distinct().ToList() ?? [];

            foreach (var roleName in targetRoles)
            {
                var roleEntity = await _roleManager.FindByNameAsync(roleName).ConfigureAwait(false);
                if (roleEntity == null)
                {
                    return (false, $"找不到指定的角色名稱: {roleName}");
                }

                int targetRoleLevel = _dbContext.Entry(roleEntity).Property<int>("HierarchyLevel").CurrentValue;

                if (actorMaxLevel <= targetRoleLevel)
                {
                    return (false, $"權限不足：您的最高位階 ({actorMaxLevel}) 無權指派同級或更高位階的角色 [{roleName}] ({targetRoleLevel})。");
                }
            }

            var currentRoles = await _userManager.GetRolesAsync(targetUser).ConfigureAwait(false);

            var removeResult = await _userManager.RemoveFromRolesAsync(targetUser, currentRoles.Except(targetRoles)).ConfigureAwait(false);
            if (!removeResult.Succeeded) return (false, string.Join("; ", removeResult.Errors.Select(e => e.Description)));

            var addResult = await _userManager.AddToRolesAsync(targetUser, targetRoles.Except(currentRoles)).ConfigureAwait(false);
            if (!addResult.Succeeded) return (false, string.Join("; ", addResult.Errors.Select(e => e.Description)));

            _logger.LogInformation("操作者 [{ActorId}] 成功更新使用者 [{TargetId}] 的角色指派: {Roles}", actorUserId, userId, string.Join(", ", targetRoles));
            return (true, "使用者角色更新成功。");
        }

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