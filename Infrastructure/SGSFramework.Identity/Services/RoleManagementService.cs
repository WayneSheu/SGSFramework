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
    using SGSFramework.AuthTokenBucket.Services;
    using SGSFramework.Core.Abstractions.Adapters;
    using SGSFramework.Core.Abstractions.Entities.Identities;
    using SGSFramework.Core.Abstractions.Permissions;
    using SGSFramework.Core.Abstractions.Permissions.Entities;
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
        private readonly IPermissionChangeNotifier _permissionChangeNotifier;
        private readonly IRoleAuditService _roleAuditService;
        private readonly IRoleExpirationService _roleExpirationService;
        private readonly IRoleGovernanceService _roleGovernanceService;
        private readonly ILogger<RoleManagementService<TRole, TKey>> _logger;

        public RoleManagementService(
            RoleManager<TRole> roleManager,
            UserManager<ApplicationUser> userManager,
            IOrganizationIntegrationService orgIntegrationService,
            IPermissionManagementService permissionService,
            IPermissionRegistry permissionRegistry,
            IHttpContextAccessor httpContextAccessor,
            DbContext dbContext,
            IPermissionChangeNotifier permissionChangeNotifier,
            IRoleAuditService roleAuditService,
            IRoleExpirationService roleExpirationService,
            IRoleGovernanceService roleGovernanceService,
            ILogger<RoleManagementService<TRole, TKey>> logger)
        {
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _orgIntegrationService = orgIntegrationService ?? throw new ArgumentNullException(nameof(orgIntegrationService));
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
            _permissionRegistry = permissionRegistry ?? throw new ArgumentNullException(nameof(permissionRegistry));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _permissionChangeNotifier = permissionChangeNotifier ?? throw new ArgumentNullException(nameof(permissionChangeNotifier));
            _roleAuditService = roleAuditService ?? throw new ArgumentNullException(nameof(roleAuditService));
            _roleExpirationService = roleExpirationService ?? throw new ArgumentNullException(nameof(roleExpirationService));
            _roleGovernanceService = roleGovernanceService ?? throw new ArgumentNullException(nameof(roleGovernanceService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 驗證角色範圍類型 (ScopeType) 與管理階層數值 (HierarchyLevel) 以及實驗室繫結之間的聯動防呆規則
        /// </summary>
        /// <summary>角色代碼：選填；有值時須為英數、底線或連字號（最多 64 字），且不得與其他未刪除角色重複（不分大小寫）</summary>
        private async Task<string?> ValidateRoleCodeAsync(string? code, string? excludeRoleId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;

            var trimmed = code.Trim();
            if (trimmed.Length > 64 || !System.Text.RegularExpressions.Regex.IsMatch(trimmed, "^[A-Za-z0-9_-]+$"))
            {
                return "角色代碼僅能使用英文字母、數字、底線或連字號，且不超過 64 個字元。";
            }

            var normalized = trimmed.ToUpperInvariant();
            var roles = await _roleManager.Roles.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            bool taken = roles.Any(r =>
            {
                var entry = _dbContext.Entry(r);
                if (entry.Property<bool>("IsDeleted").CurrentValue) return false;
                if (excludeRoleId is not null && string.Equals(r.Id?.ToString(), excludeRoleId, StringComparison.OrdinalIgnoreCase)) return false;
                return string.Equals((entry.Property<string>("Code").CurrentValue ?? string.Empty).Trim(), normalized, StringComparison.OrdinalIgnoreCase);
            });

            return taken ? $"角色代碼「{trimmed}」已被其他角色使用。" : null;
        }

        private static List<Guid> NormalizeLabIds(IEnumerable<Guid>? labIds, Guid? singleLabId) =>
            (labIds ?? (singleLabId.HasValue ? [singleLabId.Value] : []))
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

        /// <summary>讀取角色適用的區域實驗室；尚未建立對應資料的舊角色以 TenantLabId 代替</summary>
        private async Task<List<Guid>> LoadRoleLabIdsAsync(Guid roleId, Guid? fallbackLabId, CancellationToken cancellationToken)
        {
            var labIds = await _dbContext.Set<RoleLabScope>().AsNoTracking()
                .Where(x => x.RoleId == roleId)
                .Select(x => x.TenantLabId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return labIds.Count > 0 ? labIds : NormalizeLabIds(null, fallbackLabId);
        }

        private async Task<Dictionary<Guid, List<Guid>>> LoadAllRoleLabIdsAsync(CancellationToken cancellationToken) =>
            (await _dbContext.Set<RoleLabScope>().AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false))
                .GroupBy(x => x.RoleId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.TenantLabId).ToList());

        public async Task<(bool Succeeded, string Message)> SetRoleLabScopeAsync(
            string roleId,
            Guid tenantLabId,
            bool assign,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(roleId);
            if (tenantLabId == Guid.Empty) return (false, "實驗室識別碼無效。");

            var role = await _roleManager.FindByIdAsync(roleId).ConfigureAwait(false);
            if (role == null || _dbContext.Entry(role).Property<bool>("IsDeleted").CurrentValue)
            {
                return (false, "找不到指定的角色。");
            }

            var entry = _dbContext.Entry(role);
            if (entry.Property<RoleScopeType>("ScopeType").CurrentValue != RoleScopeType.LabScoped)
            {
                return (false, $"角色 [{role.Name}] 不是實驗室專屬角色，無法指派區域實驗室。");
            }
            if (entry.Property<bool>("IsSystemRole").CurrentValue)
            {
                return (false, "系統內建預設角色受到保護，禁止修改。");
            }
            if (IsLocked(role))
            {
                return (false, LockedMessage(role, "調整適用實驗室"));
            }

            var roleGuid = RoleGuid(role);
            var labIds = await LoadRoleLabIdsAsync(roleGuid, entry.Property<Guid?>("TenantLabId").CurrentValue, cancellationToken).ConfigureAwait(false);

            if (assign == labIds.Contains(tenantLabId))
            {
                return (true, assign ? "角色已指派此實驗室。" : "角色未指派此實驗室。");
            }

            if (assign) labIds.Add(tenantLabId);
            else labIds.Remove(tenantLabId);

            if (labIds.Count == 0)
            {
                return (false, $"角色 [{role.Name}] 至少須保留一個區域實驗室；如不再使用請停用或刪除角色。");
            }

            await SaveRoleLabScopesAsync(roleGuid, labIds, cancellationToken).ConfigureAwait(false);
            entry.Property("TenantLabId").CurrentValue = labIds[0];
            var result = await _roleManager.UpdateAsync(role).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                return (false, string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            await _roleAuditService.RecordAsync(roleGuid, role.Name!, RoleAuditActions.Updated,
                $"經由實驗室管理{(assign ? "加入" : "移除")}適用實驗室，目前共 {labIds.Count} 個",
                detail: new { TenantLabId = tenantLabId, Assigned = assign, TenantLabIds = labIds },
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await _permissionChangeNotifier.NotifyRoleMembersAsync(role.Name!, "role-lab-scope-changed", cancellationToken).ConfigureAwait(false);

            return (true, assign ? $"已將角色 [{role.Name}] 指派至此實驗室。" : $"已自此實驗室移除角色 [{role.Name}]。");
        }

        private async Task SaveRoleLabScopesAsync(Guid roleId, IReadOnlyCollection<Guid> labIds, CancellationToken cancellationToken)
        {
            var set = _dbContext.Set<RoleLabScope>();
            var existing = await set.Where(x => x.RoleId == roleId).ToListAsync(cancellationToken).ConfigureAwait(false);

            set.RemoveRange(existing.Where(x => !labIds.Contains(x.TenantLabId)));
            set.AddRange(labIds.Where(id => existing.All(x => x.TenantLabId != id))
                .Select(id => new RoleLabScope { RoleId = roleId, TenantLabId = id }));

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        private static string FormatPeriod(DateTimeOffset? from, DateTimeOffset? to) =>
            $"{(from.HasValue ? from.Value.ToLocalTime().ToString("yyyy-MM-dd") : "立即")} ～ {(to.HasValue ? to.Value.ToLocalTime().ToString("yyyy-MM-dd") : "無期限")}";

        private static (bool IsValid, string ErrorMessage) ValidateEffectivePeriod(DateTimeOffset? from, DateTimeOffset? to) =>
            from.HasValue && to.HasValue && to.Value < from.Value
                ? (false, "生效迄日不可早於生效起日。")
                : (true, string.Empty);

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
                var roleLabIds = await LoadAllRoleLabIdsAsync(cancellationToken).ConfigureAwait(false);
                var roleDtos = new List<RoleDto>(roles.Count);

                foreach (var role in roles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string roleIdStr = role.Id!.ToString()!;

                    var expirations = await _roleExpirationService.GetByRoleAsync(RoleGuid(role), cancellationToken).ConfigureAwait(false);
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
                                    PrimaryLabName = primaryLabName,
                                    ExpiresAt = Guid.TryParse(userIdStr, out var memberGuid1) && expirations.TryGetValue(memberGuid1, out var exp1) ? exp1.ExpiresAt : null,
                                    ExpirationReason = Guid.TryParse(userIdStr, out var memberGuid2) && expirations.TryGetValue(memberGuid2, out var exp2) ? exp2.Reason : null
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
                        TenantLabIds = roleLabIds.TryGetValue(RoleGuid(role), out var scopedLabs) ? scopedLabs : NormalizeLabIds(null, tenantLabId),
                        EffectiveFrom = entry.Property<DateTimeOffset?>("EffectiveFrom").CurrentValue,
                        EffectiveTo = entry.Property<DateTimeOffset?>("EffectiveTo").CurrentValue,
                        IsSystemRole = isSystemRole,
                        Status = statusVal.GetDisplayName(),
                        StatusValue = (int)statusVal,
                        HasPendingPermissionChange = await HasPendingPermissionChangeAsync(roleIdStr, cancellationToken).ConfigureAwait(false),
                        IsDeleted = isDeleted,
                        MappedAdGroups = await GetMappedAdGroupsAsync(role).ConfigureAwait(false),
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

                var expirations = await _roleExpirationService.GetByRoleAsync(RoleGuid(role), cancellationToken).ConfigureAwait(false);
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
                            PrimaryLabName = primaryLabName,
                            ExpiresAt = Guid.TryParse(userIdStr, out var memberGuid1) && expirations.TryGetValue(memberGuid1, out var exp1) ? exp1.ExpiresAt : null,
                            ExpirationReason = Guid.TryParse(userIdStr, out var memberGuid2) && expirations.TryGetValue(memberGuid2, out var exp2) ? exp2.Reason : null
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
                    TenantLabIds = await LoadRoleLabIdsAsync(RoleGuid(role), tenantLabId, cancellationToken).ConfigureAwait(false),
                    EffectiveFrom = entry.Property<DateTimeOffset?>("EffectiveFrom").CurrentValue,
                    EffectiveTo = entry.Property<DateTimeOffset?>("EffectiveTo").CurrentValue,
                    IsSystemRole = isSystemRole,
                    Status = statusVal.GetDisplayName(),
                    StatusValue = (int)statusVal,
                    HasPendingPermissionChange = await HasPendingPermissionChangeAsync(role.Id!.ToString()!, cancellationToken).ConfigureAwait(false),
                    IsDeleted = false,
                    MappedAdGroups = await GetMappedAdGroupsAsync(role).ConfigureAwait(false),
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
                // 實驗室專屬角色可指派多個區域實驗室；TenantLabId 保留第一個供相容用途
                var labIds = scopeType == RoleScopeType.LabScoped ? NormalizeLabIds(request.TenantLabIds, request.TenantLabId) : [];
                Guid? tenantLabId = labIds.Count > 0 ? labIds[0] : null;

                // 執行範圍與階層聯動防呆驗證
                var (isValid, errorMessage) = ValidateRoleScopeAndHierarchy(scopeType, hierarchyLevel, tenantLabId);
                if (!isValid)
                {
                    return (false, new[] { errorMessage });
                }

                var (isPeriodValid, periodError) = ValidateEffectivePeriod(request.EffectiveFrom, request.EffectiveTo);
                if (!isPeriodValid)
                {
                    return (false, new[] { periodError });
                }

                var codeError = await ValidateRoleCodeAsync(request.Code, null, cancellationToken).ConfigureAwait(false);
                if (codeError is not null)
                {
                    return (false, new[] { codeError });
                }

                var role = new TRole
                {
                    Name = request.RoleName,
                    NormalizedName = _roleManager.NormalizeKey(request.RoleName)
                };

                var entry = _dbContext.Entry(role);
                entry.Property("Code").CurrentValue = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim().ToUpperInvariant();
                entry.Property("Description").CurrentValue = request.Description;
                entry.Property("ScopeType").CurrentValue = scopeType;
                entry.Property("HierarchyLevel").CurrentValue = hierarchyLevel;
                entry.Property("TenantLabId").CurrentValue = tenantLabId;
                entry.Property("EffectiveFrom").CurrentValue = request.EffectiveFrom;
                entry.Property("EffectiveTo").CurrentValue = request.EffectiveTo;
                entry.Property("IsSystemRole").CurrentValue = request.IsSystemRole;
                entry.Property("IsDeleted").CurrentValue = false;
                // 新建角色預設「待審核」：須經他人核准後才啟用（未啟用雙重審批時直接啟用）
                var initialStatus = _roleGovernanceService.RequireApproval ? RoleStatus.PendingApproval : RoleStatus.Active;
                entry.Property("Status").CurrentValue = initialStatus;

                var result = await _roleManager.CreateAsync(role).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    await SaveRoleLabScopesAsync(RoleGuid(role), labIds, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("成功建立角色：{RoleName} (ID: {RoleId}, Scope: {Scope}, Level: {Level})",
                        role.Name, role.Id, scopeType, hierarchyLevel);
                    await _roleAuditService.RecordAsync(RoleGuid(role), role.Name!, RoleAuditActions.Created,
                        $"建立角色（範圍：{scopeType.GetDisplayName()}，階層：{hierarchyLevel}，狀態：{initialStatus.GetDisplayName()}）", cancellationToken: cancellationToken).ConfigureAwait(false);
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

                if (IsLocked(role))
                {
                    return (false, new[] { LockedMessage(role, "修改角色設定") });
                }

                string oldName = role.Name ?? string.Empty;
                string oldDescription = entry.Property<string>("Description").CurrentValue ?? string.Empty;
                var oldScope = entry.Property<RoleScopeType>("ScopeType").CurrentValue;
                var oldLabId = entry.Property<Guid?>("TenantLabId").CurrentValue;
                var oldFrom = entry.Property<DateTimeOffset?>("EffectiveFrom").CurrentValue;
                var oldTo = entry.Property<DateTimeOffset?>("EffectiveTo").CurrentValue;

                // 若 Request 帶有 ScopeType、HierarchyLevel、TenantLabId 變更，進行聯動防呆驗證
                var targetScope = request.ScopeType != default ? request.ScopeType : entry.Property<RoleScopeType>("ScopeType").CurrentValue;
                var targetLevel = request.HierarchyLevel != 0 ? request.HierarchyLevel : entry.Property<int>("HierarchyLevel").CurrentValue;
                // 非實驗室專屬角色不得繫結實驗室：切換範圍時一併清除，避免沿用舊值導致驗證失敗
                var oldLabIds = await LoadRoleLabIdsAsync(RoleGuid(role), oldLabId, cancellationToken).ConfigureAwait(false);
                var targetLabIds = targetScope != RoleScopeType.LabScoped
                    ? []
                    : request.TenantLabIds is not null || request.TenantLabId.HasValue
                        ? NormalizeLabIds(request.TenantLabIds, request.TenantLabId)
                        : oldLabIds;
                Guid? targetLabId = targetLabIds.Count > 0 ? targetLabIds[0] : null;

                var (isValid, errorMessage) = ValidateRoleScopeAndHierarchy(targetScope, targetLevel, targetLabId);
                if (!isValid)
                {
                    return (false, new[] { errorMessage });
                }

                var (isPeriodValid, periodError) = ValidateEffectivePeriod(request.EffectiveFrom, request.EffectiveTo);
                if (!isPeriodValid)
                {
                    return (false, new[] { periodError });
                }

                var codeError = await ValidateRoleCodeAsync(request.Code, request.RoleId, cancellationToken).ConfigureAwait(false);
                if (codeError is not null)
                {
                    return (false, new[] { codeError });
                }

                role.Name = request.NewRoleName;
                role.NormalizedName = _roleManager.NormalizeKey(request.NewRoleName);

                // 與 ScopeType 等欄位一致：未提供時保留原值，避免僅編輯描述時誤清空角色代碼
                if (!string.IsNullOrWhiteSpace(request.Code))
                {
                    entry.Property("Code").CurrentValue = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim().ToUpperInvariant();
                }
                entry.Property("Description").CurrentValue = request.Description;
                entry.Property("ScopeType").CurrentValue = targetScope;
                entry.Property("HierarchyLevel").CurrentValue = targetLevel;
                entry.Property("TenantLabId").CurrentValue = targetLabId;
                entry.Property("EffectiveFrom").CurrentValue = request.EffectiveFrom;
                entry.Property("EffectiveTo").CurrentValue = request.EffectiveTo;

                var result = await _roleManager.UpdateAsync(role).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    await SaveRoleLabScopesAsync(RoleGuid(role), targetLabIds, cancellationToken).ConfigureAwait(false);
                    var changes = new List<string>();
                    if (!string.Equals(oldName, role.Name, StringComparison.Ordinal)) changes.Add($"名稱「{oldName}」→「{role.Name}」");
                    if (!string.Equals(oldDescription, request.Description ?? string.Empty, StringComparison.Ordinal)) changes.Add("描述已更新");
                    if (oldScope != targetScope) changes.Add($"範圍「{oldScope.GetDisplayName()}」→「{targetScope.GetDisplayName()}」");
                    if (!oldLabIds.ToHashSet().SetEquals(targetLabIds)) changes.Add($"適用實驗室 {oldLabIds.Count} → {targetLabIds.Count} 個");
                    if (oldFrom != request.EffectiveFrom || oldTo != request.EffectiveTo)
                        changes.Add($"生效期間：{FormatPeriod(request.EffectiveFrom, request.EffectiveTo)}");
                    await _roleAuditService.RecordAsync(RoleGuid(role), role.Name!, RoleAuditActions.Updated,
                        changes.Count > 0 ? string.Join("；", changes) : "更新角色設定",
                        detail: new { OldDescription = oldDescription, NewDescription = request.Description, Scope = targetScope.ToString(), TenantLabIds = targetLabIds, request.EffectiveFrom, request.EffectiveTo },
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    // 範圍、實驗室或生效期間可能改變成員的有效權限
                    await _permissionChangeNotifier.NotifyRoleMembersAsync(role.Name!, "role-updated", cancellationToken).ConfigureAwait(false);
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
                var oldStatus = entry.Property<RoleStatus>("Status").CurrentValue;
                if (oldStatus == newStatus)
                {
                    return (true, Enumerable.Empty<string>());
                }

                var isPendingReview = oldStatus == RoleStatus.PendingApproval;
                if (newStatus == RoleStatus.PendingApproval)
                {
                    return (false, new[] { "「待審核」僅適用於新建角色，無法將既有角色改回待審核。" });
                }

                var roleGuid = RoleGuid(role);
                if (await _dbContext.Set<RoleChangeRequest>().AsNoTracking()
                        .AnyAsync(x => x.RoleId == roleGuid && x.Status == RoleChangeRequestStatus.Pending, cancellationToken)
                        .ConfigureAwait(false))
                {
                    return (false, new[] { "此角色有待審核的權限變更申請，請至「權限變更審核」核准、駁回或撤回後，狀態將自動更新。" });
                }

                if (isPendingReview)
                {
                    // 待審核角色僅能「核准啟用」或「駁回停用」
                    if (newStatus == RoleStatus.Locked)
                    {
                        return (false, new[] { "待審核角色僅能核准（啟用中）或駁回（已停用）。" });
                    }

                    if (newStatus == RoleStatus.Active)
                    {
                        var approvalError = await ValidateNewRoleApprovalAsync(role, cancellationToken).ConfigureAwait(false);
                        if (approvalError is not null)
                        {
                            return (false, new[] { approvalError });
                        }
                    }
                }
                else if (entry.Property<bool>("IsSystemRole").CurrentValue && newStatus == RoleStatus.Disabled)
                {
                    // 駁回待審核的系統角色不受此限
                    return (false, new[] { "系統核心預設角色禁止停用。" });
                }

                entry.Property("Status").CurrentValue = newStatus;

                var result = await _roleManager.UpdateAsync(role).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    // 停用：成員立即失去此角色權限；啟用 / 鎖定：成員權限恢復或維持 —— 一律重新計算並通知
                    int memberCount = 0;
                    if (role.Name is not null)
                    {
                        memberCount = (await _userManager.GetUsersInRoleAsync(role.Name).ConfigureAwait(false)).Count;
                        await _permissionChangeNotifier.NotifyRoleMembersAsync(role.Name, "role-status-changed", cancellationToken).ConfigureAwait(false);
                    }

                    var summary = isPendingReview
                        ? (newStatus == RoleStatus.Active ? "新角色審核通過，已啟用" : "新角色審核駁回，已停用")
                        : $"狀態「{oldStatus.GetDisplayName()}」→「{newStatus.GetDisplayName()}」，影響 {memberCount} 位成員";
                    await _roleAuditService.RecordAsync(RoleGuid(role), role.Name ?? string.Empty, RoleAuditActions.StatusChanged,
                        summary,
                        memberCount, new { OldStatus = oldStatus.ToString(), NewStatus = newStatus.ToString() },
                        cancellationToken).ConfigureAwait(false);
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

                if (IsLocked(role))
                {
                    return (false, new[] { LockedMessage(role, "刪除角色") });
                }

                string originalName = role.Name ?? string.Empty;

                // 刪除前記錄成員，刪除後通知其權限已變更
                var affectedUsers = role.Name is null
                    ? []
                    : await _userManager.GetUsersInRoleAsync(role.Name).ConfigureAwait(false);
                var affectedUserIds = affectedUsers.Select(u => u.Id.ToString()).ToList();

                // 一併解除成員關係，避免使用者仍掛著已刪除角色（稽核畫面誤顯示繼承權限）
                foreach (var member in affectedUsers)
                {
                    var removeResult = await _userManager.RemoveFromRoleAsync(member, role.Name!).ConfigureAwait(false);
                    if (!removeResult.Succeeded)
                    {
                        _logger.LogWarning("刪除角色 {RoleName} 時移除成員 {UserId} 失敗：{Errors}",
                            role.Name, member.Id, string.Join("; ", removeResult.Errors.Select(e => e.Description)));
                    }
                }

                string anonymizedTag = roleId.Length >= 8 ? roleId[..8] : roleId;

                entry.Property("IsDeleted").CurrentValue = true;
                entry.Property("Code").CurrentValue = $"deleted_code_{anonymizedTag}";
                role.Name = $"deleted_role_{anonymizedTag}";
                role.NormalizedName = $"DELETED_ROLE_{anonymizedTag.ToUpperInvariant()}";

                var result = await _roleManager.UpdateAsync(role).ConfigureAwait(false);
                if (result.Succeeded)
                {
                    await _permissionChangeNotifier.NotifyUsersAsync(affectedUserIds, "role-deleted", cancellationToken).ConfigureAwait(false);
                    await _roleAuditService.RecordAsync(RoleGuid(role), originalName, RoleAuditActions.Deleted,
                        $"刪除角色，{affectedUserIds.Count} 位成員失去此角色權限", affectedUserIds.Count,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
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

                // 已停用 / 已鎖定的角色不再授予任何權限：禁止新指派（既有成員保留不受影響）
                var roleStatus = _dbContext.Entry(roleEntity).Property<RoleStatus>("Status").CurrentValue;
                if (roleStatus is not (RoleStatus.Active or RoleStatus.PendingApproval) &&
                    !await _userManager.IsInRoleAsync(targetUser, roleName).ConfigureAwait(false))
                {
                    return (false, $"角色 [{roleName}] 目前為「{roleStatus.GetDisplayName()}」，無法指派給使用者。");
                }
            }

            var currentRoles = await _userManager.GetRolesAsync(targetUser).ConfigureAwait(false);

            var removeResult = await _userManager.RemoveFromRolesAsync(targetUser, currentRoles.Except(targetRoles)).ConfigureAwait(false);
            if (!removeResult.Succeeded) return (false, string.Join("; ", removeResult.Errors.Select(e => e.Description)));

            var addResult = await _userManager.AddToRolesAsync(targetUser, targetRoles.Except(currentRoles)).ConfigureAwait(false);
            if (!addResult.Succeeded) return (false, string.Join("; ", addResult.Errors.Select(e => e.Description)));

            _logger.LogInformation("操作者 [{ActorId}] 成功更新使用者 [{TargetId}] 的角色指派: {Roles}", actorUserId, userId, string.Join(", ", targetRoles));
            await _permissionChangeNotifier.NotifyUsersAsync([userId], "user-roles-updated").ConfigureAwait(false);

            // 於各角色的異動歷程留下紀錄（由使用者管理端調整）
            foreach (var (roleName, action, verb) in currentRoles.Except(targetRoles).Select(r => (r, RoleAuditActions.MembersRemoved, "移除"))
                         .Concat(targetRoles.Except(currentRoles).Select(r => (r, RoleAuditActions.MembersAdded, "加入"))))
            {
                var changedRole = await _roleManager.FindByNameAsync(roleName).ConfigureAwait(false);
                if (changedRole is null) continue;
                await _roleAuditService.RecordAsync(RoleGuid(changedRole), roleName, action,
                    $"經由使用者管理{verb}成員：{targetUser.UserName}", 1, new { UserIds = new[] { userId } }).ConfigureAwait(false);
            }
            return (true, "使用者角色更新成功。");
        }

        private static Guid RoleGuid(TRole role) =>
            Guid.TryParse(role.Id?.ToString(), out var id) ? id : Guid.Empty;

        /// <summary>角色是否有待審核的權限變更申請（送審期間角色狀態為「待審核」）</summary>
        private Task<bool> HasPendingPermissionChangeAsync(string roleId, CancellationToken cancellationToken) =>
            Guid.TryParse(roleId, out var id)
                ? _dbContext.Set<RoleChangeRequest>().AsNoTracking().AnyAsync(x => x.RoleId == id && x.Status == RoleChangeRequestStatus.Pending, cancellationToken)
                : Task.FromResult(false);

        /// <summary>
        /// 新角色審核（雙重審批）：建立者不得核准自己建立的角色；
        /// 例外與權限變更審核一致 —— 持有啟用中的系統內建角色且最高管理階層高於該角色者可自行核准。
        /// </summary>
        private async Task<string?> ValidateNewRoleApprovalAsync(TRole role, CancellationToken cancellationToken)
        {
            var actorValue = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? _httpContextAccessor.HttpContext?.User?.FindFirst("sub")?.Value;
            if (!Guid.TryParse(actorValue, out var actorId))
            {
                return "無法識別目前操作者的身分驗證資訊。";
            }

            var roleId = RoleGuid(role);
            var creatorId = await _dbContext.Set<RoleAuditLog>().AsNoTracking()
                .Where(l => l.RoleId == roleId && l.Action == RoleAuditActions.Created)
                .OrderBy(l => l.CreatedAt)
                .Select(l => l.ActorUserId)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (creatorId == actorId &&
                !await _roleGovernanceService.CanSelfApproveAsync(actorId, roleId, cancellationToken).ConfigureAwait(false))
            {
                return "雙重審批規定：角色建立者不得核准自己建立的角色，請由其他審核者處理。" +
                       "（持有系統內建角色且管理階層高於此角色者除外）";
            }

            return null;
        }

        /// <summary>鎖定 = 凍結角色配置：禁止修改設定、權限、AD 群組、新增成員與刪除（移除成員不受限）</summary>
        private bool IsLocked(TRole role) =>
            _dbContext.Entry(role).Property<RoleStatus>("Status").CurrentValue == RoleStatus.Locked;

        private static string LockedMessage(TRole role, string operation) =>
            $"角色 [{role.Name}] 目前為「已鎖定」（配置凍結中），無法{operation}。如需調整請先解除鎖定。";

        /// <summary>
        /// AD 群組對應以角色宣告 (AspNetRoleClaims) 儲存，不需額外資料表；
        /// 權限相關邏輯皆以宣告類型過濾，與此類型互不影響。
        /// </summary>
        private const string AdGroupClaimType = "ad_group";

        private async Task<List<string>> GetMappedAdGroupsAsync(TRole role)
        {
            var claims = await _roleManager.GetClaimsAsync(role).ConfigureAwait(false);
            return claims
                .Where(c => c.Type == AdGroupClaimType && !string.IsNullOrWhiteSpace(c.Value))
                .Select(c => c.Value)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<(bool Succeeded, string Message)> MapAdGroupToRoleAsync(
            MapAdGroupToRoleRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var groupName = request.AdGroupName?.Trim();
            if (string.IsNullOrEmpty(groupName))
            {
                return (false, "AD 群組名稱不可為空。");
            }

            var role = await _roleManager.FindByIdAsync(request.RoleId).ConfigureAwait(false);
            if (role is null)
            {
                return (false, $"找不到識別碼為 '{request.RoleId}' 的角色。");
            }

            if (IsLocked(role))
            {
                return (false, LockedMessage(role, "異動 AD 群組對應"));
            }

            var existing = await GetMappedAdGroupsAsync(role).ConfigureAwait(false);
            if (existing.Contains(groupName, StringComparer.OrdinalIgnoreCase))
            {
                return (false, $"AD 群組「{groupName}」已對應至此角色。");
            }

            var result = await _roleManager.AddClaimAsync(role, new Claim(AdGroupClaimType, groupName)).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                return (false, string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            _logger.LogInformation("角色 [{RoleId}] 已新增 AD 群組對應：{AdGroup}", request.RoleId, groupName);
            await _roleAuditService.RecordAsync(RoleGuid(role), role.Name ?? string.Empty, RoleAuditActions.AdGroupMapped,
                $"新增 AD 群組對應「{groupName}」", cancellationToken: cancellationToken).ConfigureAwait(false);
            return (true, "AD 群組對應設定成功。");
        }

        public async Task<(bool Succeeded, string Message)> RemoveAdGroupFromRoleAsync(
            RemoveAdGroupFromRoleRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var groupName = request.AdGroupName?.Trim();
            if (string.IsNullOrEmpty(groupName))
            {
                return (false, "AD 群組名稱不可為空。");
            }

            var role = await _roleManager.FindByIdAsync(request.RoleId).ConfigureAwait(false);
            if (role is null)
            {
                return (false, $"找不到識別碼為 '{request.RoleId}' 的角色。");
            }

            if (IsLocked(role))
            {
                return (false, LockedMessage(role, "異動 AD 群組對應"));
            }

            var claims = await _roleManager.GetClaimsAsync(role).ConfigureAwait(false);
            var target = claims.FirstOrDefault(c =>
                c.Type == AdGroupClaimType && string.Equals(c.Value, groupName, StringComparison.OrdinalIgnoreCase));
            if (target is null)
            {
                return (false, $"此角色未對應 AD 群組「{groupName}」。");
            }

            var result = await _roleManager.RemoveClaimAsync(role, target).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                return (false, string.Join("; ", result.Errors.Select(e => e.Description)));
            }

            _logger.LogInformation("角色 [{RoleId}] 已移除 AD 群組對應：{AdGroup}", request.RoleId, groupName);
            await _roleAuditService.RecordAsync(RoleGuid(role), role.Name ?? string.Empty, RoleAuditActions.AdGroupRemoved,
                $"移除 AD 群組對應「{groupName}」", cancellationToken: cancellationToken).ConfigureAwait(false);
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

            // 已停用 / 已鎖定的角色不再授予任何權限，因此禁止加入新成員（待審核可先指派，核准後才生效）
            var roleStatus = _dbContext.Entry(role).Property<RoleStatus>("Status").CurrentValue;
            if (roleStatus is not (RoleStatus.Active or RoleStatus.PendingApproval))
            {
                return (false, $"角色 [{role.Name}] 目前為「{roleStatus.GetDisplayName()}」，無法加入新成員。", null);
            }

            if (request.ExpiresAt is { } requestedExpiry && requestedExpiry <= DateTimeOffset.UtcNow)
            {
                return (false, "過渡期到期時間必須晚於現在。", null);
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
                await _permissionChangeNotifier.NotifyUsersAsync(users.Select(u => u.Id.ToString()), "role-members-added", cancellationToken).ConfigureAwait(false);
                await _roleAuditService.RecordAsync(RoleGuid(role), role.Name ?? string.Empty, RoleAuditActions.MembersAdded,
                    $"加入 {users.Count} 位成員：{string.Join("、", users.Select(u => u.UserName))}", users.Count,
                    new { UserIds = users.Select(u => u.Id.ToString()) }, cancellationToken).ConfigureAwait(false);

                // 過渡期：設定到期自動回收；未指定則為永久成員（清除既有期限）
                var addedGuids = users.Select(u => Guid.Parse(u.Id.ToString()!)).ToList();
                if (request.ExpiresAt.HasValue)
                {
                    await _roleExpirationService.SetExpirationAsync(RoleGuid(role), addedGuids, request.ExpiresAt, request.Reason, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await _roleExpirationService.ClearAsync(RoleGuid(role), addedGuids, cancellationToken).ConfigureAwait(false);
                }
                return (true, $"已成功將角色 [{role.Name}] 批次指派給 {successCount} 位使用者。", null);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        public async Task<(bool Succeeded, string Message, IEnumerable<string>? Errors)> BatchRemoveUsersFromRoleAsync(
            string roleIdentifier,
            BatchAssignUsersRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(roleIdentifier);
            ArgumentNullException.ThrowIfNull(request);

            TRole? role = Guid.TryParse(roleIdentifier, out var roleGuid)
                ? await _roleManager.FindByIdAsync(roleGuid.ToString()).ConfigureAwait(false)
                : await _roleManager.FindByNameAsync(roleIdentifier).ConfigureAwait(false);

            if (role == null || role.Name == null || _dbContext.Entry(role).Property<bool>("IsDeleted").CurrentValue)
            {
                return (false, $"找不到指定的角色 [{roleIdentifier}]。", null);
            }

            var userGuids = request.UserIds?
                .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .Distinct()
                .ToList() ?? [];

            var users = await _userManager.Users
                .Where(u => userGuids.Contains((Guid)(object)u.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (users.Count == 0) return (false, "找不到對應的使用者清單。", null);

            // 系統內建角色至少保留一位成員，避免失去最後的管理者
            if (_dbContext.Entry(role).Property<bool>("IsSystemRole").CurrentValue)
            {
                var currentMembers = await _userManager.GetUsersInRoleAsync(role.Name).ConfigureAwait(false);
                var remaining = currentMembers.Count(m => !users.Any(u => Equals(u.Id, m.Id)));
                if (remaining == 0)
                {
                    return (false, $"系統內建角色 [{role.Name}] 至少需保留一位成員。", null);
                }
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var errors = new List<string>();
                int removedCount = 0;

                foreach (var user in users)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!await _userManager.IsInRoleAsync(user, role.Name).ConfigureAwait(false))
                    {
                        continue; // 原本就不是成員，視為已完成
                    }

                    var result = await _userManager.RemoveFromRoleAsync(user, role.Name).ConfigureAwait(false);
                    if (result.Succeeded) removedCount++;
                    else errors.Add($"使用者 [{user.UserName}] 移除失敗: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }

                if (errors.Count > 0)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    return (false, "部分使用者移除失敗，交易已全數回滾。", errors);
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                await _permissionChangeNotifier.NotifyUsersAsync(users.Select(u => u.Id.ToString()), "role-members-removed", cancellationToken).ConfigureAwait(false);
                await _roleAuditService.RecordAsync(RoleGuid(role), role.Name ?? string.Empty, RoleAuditActions.MembersRemoved,
                    $"移除 {removedCount} 位成員：{string.Join("、", users.Select(u => u.UserName))}", removedCount,
                    new { UserIds = users.Select(u => u.Id.ToString()) }, cancellationToken).ConfigureAwait(false);
                await _roleExpirationService.ClearAsync(RoleGuid(role), users.Select(u => Guid.Parse(u.Id.ToString()!)), cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("已自角色 [{RoleName}] 移除 {Count} 位成員。", role.Name, removedCount);
                return (true, $"已自角色 [{role.Name}] 移除 {removedCount} 位成員。", null);
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