// ==========================================
// 檔案路徑: src/SGSFramework.Identity/Services/SystemRolePermissionSeedService.cs
// 架構層級: Identity / Services[cite: 19]
// ==========================================

#nullable enable

namespace SGSFramework.Identity.Services
{
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using SGSFramework.AuthTokenBucket.Abstractions;
    using SGSFramework.Core.Abstractions.DbContexts;
    using SGSFramework.Core.Abstractions.Entities.Controller;
    using SGSFramework.Core.Abstractions.Entities.Identities;
    using SGSFramework.Core.Abstractions.Permissions;
    using SGSFramework.Core.Abstractions.Permissions.Entities;
    using SGSFramework.Core.Abstractions.Permissions.Enums;
    using SGSFramework.Core.Abstractions.Permissions.Repositories;
    using SGSFramework.Identity.Abstractions;
    using SGSFramework.Identity.Options;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading;
    using System.Threading.Tasks;

    public class SystemRolePermissionSeedService<TDbContext> : ISystemRolePermissionSeedService
        where TDbContext : DbContext, ITokenDbContext
    {
        private readonly TDbContext _dbContext;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly IPermissionRegistry _permissionRegistry;
        private readonly IPermissionBitmaskService _bitmaskService;
        private readonly IRolePermissionRepository _rolePermissionRepository;
        private readonly SystemRolePermissionSeedOptions _seedOptions;
        private readonly ILogger<SystemRolePermissionSeedService<TDbContext>> _logger;

        private const string ClaimTypePermission = "Permission";

        public SystemRolePermissionSeedService(
            TDbContext dbContext,
            RoleManager<ApplicationRole> roleManager,
            IPermissionRegistry permissionRegistry,
            IPermissionBitmaskService bitmaskService,
            IRolePermissionRepository rolePermissionRepository,
            IOptions<SystemRolePermissionSeedOptions> seedOptions,
            ILogger<SystemRolePermissionSeedService<TDbContext>> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _permissionRegistry = permissionRegistry ?? throw new ArgumentNullException(nameof(permissionRegistry));
            _bitmaskService = bitmaskService ?? throw new ArgumentNullException(nameof(bitmaskService));
            _rolePermissionRepository = rolePermissionRepository ?? throw new ArgumentNullException(nameof(rolePermissionRepository));
            _seedOptions = seedOptions?.Value ?? throw new ArgumentNullException(nameof(seedOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SeedPermissionsFromExcelAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("[SystemRolePermissionSeed] 開始執行權限元數據同步與角色授權矩陣初始化作業...");

                cancellationToken.ThrowIfCancellationRequested();

                var rawPermissions = _permissionRegistry.GetAllPermissions();
                if (rawPermissions == null || rawPermissions.Count == 0)
                {
                    _logger.LogWarning("[SystemRolePermissionSeed] 從權限註冊表中未取得任何權限定義，略過同步。");
                    return;
                }

                var registeredPermissions = rawPermissions.ToList();

                var controllerMetadatas = await _dbContext.Set<ControllerMetadata>()
                    .AsNoTracking()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                foreach (var perm in registeredPermissions)
                {
                    string key = perm.PermissionKey;
                    int bitPosition = perm.BitPosition;
                    string controllerName = perm.ControllerName ?? string.Empty;
                    string actionName = perm.ActionName ?? string.Empty;

                    var matchedMeta = controllerMetadatas.FirstOrDefault(c =>
                        c.ControllerName.Equals(controllerName, StringComparison.OrdinalIgnoreCase) &&
                        c.ActionName.Equals(actionName, StringComparison.OrdinalIgnoreCase))
                        ?? controllerMetadatas.FirstOrDefault(c =>
                        c.PermissionKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                        ?? controllerMetadatas.FirstOrDefault(c =>
                        c.ControllerName.Equals(controllerName, StringComparison.OrdinalIgnoreCase));

                    string moduleName = !string.IsNullOrEmpty(matchedMeta?.ModuleName)
                        ? matchedMeta.ModuleName
                        : (!string.IsNullOrEmpty(perm.ModuleName) ? perm.ModuleName : "SGSFramework.System");

                    string fallbackModuleName = moduleName
                        .Replace("SGSFramework.", "", StringComparison.OrdinalIgnoreCase)
                        .Replace("PhysLIMS.", "", StringComparison.OrdinalIgnoreCase);

                    string moduleTitle = !string.IsNullOrEmpty(matchedMeta?.ModuleTitle)
                        ? matchedMeta.ModuleTitle
                        : (!string.IsNullOrEmpty(perm.ModuleTitle) ? perm.ModuleTitle : fallbackModuleName);

                    string controllerTitle = !string.IsNullOrEmpty(matchedMeta?.ControllerTitle)
                        ? matchedMeta.ControllerTitle
                        : (!string.IsNullOrEmpty(perm.ControllerTitle) ? perm.ControllerTitle : controllerName);

                    string actionTitle = !string.IsNullOrEmpty(matchedMeta?.DisplayName)
                        ? matchedMeta.DisplayName
                        : (!string.IsNullOrEmpty(perm.ActionTitle) ? perm.ActionTitle : actionName);

                    string defaultCalculatedTitle = (key.EndsWith(".READ", StringComparison.OrdinalIgnoreCase) || key.EndsWith("_READ", StringComparison.OrdinalIgnoreCase))
                        ? controllerTitle
                        : actionTitle;

                    string permissionTitle = !string.IsNullOrEmpty(perm.PermissionTitle)
                        ? perm.PermissionTitle
                        : defaultCalculatedTitle;

                    string description = !string.IsNullOrEmpty(matchedMeta?.Description)
                        ? matchedMeta.Description
                        : (perm.Description ?? $"Auto-scanned permission: {key}");

                    ActionCategory category = DetermineDefaultCategory(actionName, key);
                    if (perm.Category != default(ActionCategory) && perm.Category != ActionCategory.Basic)
                    {
                        category = perm.Category;
                    }

                    var existingRecord = await _dbContext.Set<PermissionMetadata>()
                        .FirstOrDefaultAsync(p => p.ControllerName == controllerName && p.ActionName == actionName, cancellationToken)
                        .ConfigureAwait(false);

                    bool isModified = false;
                    if (existingRecord != null)
                    {
                        if (!string.Equals(existingRecord.PermissionKey, key, StringComparison.Ordinal)) { existingRecord.PermissionKey = key; isModified = true; }
                        if (existingRecord.BitPosition != bitPosition) { existingRecord.BitPosition = bitPosition; isModified = true; }
                        if (!string.Equals(existingRecord.ModuleName, moduleName, StringComparison.Ordinal)) { existingRecord.ModuleName = moduleName; isModified = true; }
                        if (!string.Equals(existingRecord.ModuleTitle, moduleTitle, StringComparison.Ordinal)) { existingRecord.ModuleTitle = moduleTitle; isModified = true; }
                        if (!string.Equals(existingRecord.ControllerName, controllerName, StringComparison.Ordinal)) { existingRecord.ControllerName = controllerName; isModified = true; }
                        if (!string.Equals(existingRecord.ControllerTitle, controllerTitle, StringComparison.Ordinal)) { existingRecord.ControllerTitle = controllerTitle; isModified = true; }
                        if (!string.Equals(existingRecord.ActionName, actionName, StringComparison.Ordinal)) { existingRecord.ActionName = actionName; isModified = true; }
                        if (!string.Equals(existingRecord.ActionTitle, actionTitle, StringComparison.Ordinal)) { existingRecord.ActionTitle = actionTitle; isModified = true; }
                        if (!string.Equals(existingRecord.PermissionTitle, permissionTitle, StringComparison.Ordinal)) { existingRecord.PermissionTitle = permissionTitle; isModified = true; }
                        if (existingRecord.Category != category) { existingRecord.Category = category; isModified = true; }

                        if (string.IsNullOrEmpty(existingRecord.Description) || existingRecord.Description.StartsWith("Auto-scanned permission:"))
                        {
                            if (!string.Equals(existingRecord.Description, description, StringComparison.Ordinal))
                            {
                                existingRecord.Description = description;
                                isModified = true;
                            }
                        }
                    }
                    else
                    {
                        var newPermission = new PermissionMetadata
                        {
                            PermissionKey = key,
                            BitPosition = bitPosition,
                            ModuleName = moduleName,
                            ModuleTitle = moduleTitle,
                            ControllerName = controllerName,
                            ControllerTitle = controllerTitle,
                            ActionName = actionName,
                            ActionTitle = actionTitle,
                            PermissionTitle = permissionTitle,
                            Description = description,
                            Category = category
                        };

                        await _dbContext.Set<PermissionMetadata>().AddAsync(newPermission, cancellationToken).ConfigureAwait(false);
                        isModified = true;
                    }

                    if (isModified)
                    {
                        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

                await RecalculatePermissionHierarchyAsync(cancellationToken).ConfigureAwait(false);

                var metadataList = await _dbContext.Set<PermissionMetadata>()
                    .AsNoTracking()
                    .Where(m => m.BitPosition >= 0 && m.BitPosition <= 63)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var allPermissionCodes = metadataList
                    .Select(m => m.PermissionKey ?? string.Empty)
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();

                _logger.LogInformation("[SystemRolePermissionSeed] 成功同步權限元數據並篩選出 {Count} 筆有效權限 (0-63 Bit)，開始依據 Options 配置進行動態角色派發...", allPermissionCodes.Count);

                foreach (var rule in _seedOptions.Roles)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    _logger.LogInformation("[SystemRolePermissionSeed] 正在初始化角色: {RoleName} (代碼: {RoleCode}), 職責說明: {Description}",
                        rule.RoleName, rule.RoleCode, rule.Description);

                    List<string> assignedCodes = rule.AssignAll
                        ? allPermissionCodes
                        : metadataList.Where(m => MatchesRule(m, rule))
                                      .Select(m => m.PermissionKey!)
                                      .ToList();

                    await AssignPermissionsToRoleAsync(rule.RoleName, assignedCodes, cancellationToken);
                }

                _logger.LogInformation("[SystemRolePermissionSeed] 所有系統角色的動態權限派發與資料庫矩陣寫入作業已完成。");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SystemRolePermissionSeed] 同步與派發權限時發生未預期例外。");
                throw;
            }
        }

        private static bool MatchesRule(PermissionMetadata permission, RolePermissionAssignmentRule rule)
        {
            string key = permission.PermissionKey ?? string.Empty;
            string categoryStr = permission.Category.ToString();

            if (rule.ExcludeKeys.Any(ex => IsKeyMatched(key, ex)))
            {
                return false;
            }

            bool categoryMatched = rule.Categories.Count == 0 || rule.Categories.Contains(categoryStr, StringComparer.OrdinalIgnoreCase);
            bool keyMatched = rule.IncludeKeys.Count == 0 || rule.IncludeKeys.Any(inc => IsKeyMatched(key, inc));

            if (rule.IncludeKeys.Count > 0 && rule.Categories.Count > 0)
            {
                return keyMatched && categoryMatched;
            }

            return keyMatched || categoryMatched;
        }

        private static bool IsKeyMatched(string key, string pattern)
        {
            if (pattern.EndsWith(".*"))
            {
                string prefix = pattern[..^1];
                return key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }
            else if (pattern.StartsWith("*."))
            {
                string suffix = pattern[1..];
                return key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
            }
            return key.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        private async Task RecalculatePermissionHierarchyAsync(CancellationToken cancellationToken)
        {
            var allPermissions = await _dbContext.Set<PermissionMetadata>()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            bool hierarchyChanged = false;
            foreach (var group in allPermissions.GroupBy(p => p.ControllerName, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(group.Key)) continue;

                var readPermission = group.FirstOrDefault(p =>
                    p.PermissionKey.EndsWith(".READ", StringComparison.OrdinalIgnoreCase) ||
                    p.PermissionKey.EndsWith("_READ", StringComparison.OrdinalIgnoreCase) ||
                    p.ActionName.StartsWith("Get", StringComparison.OrdinalIgnoreCase) ||
                    p.ActionName.StartsWith("List", StringComparison.OrdinalIgnoreCase))
                    ?? group.OrderBy(p => p.BitPosition).FirstOrDefault();

                if (readPermission != null)
                {
                    foreach (var perm in group)
                    {
                        int? targetParentId = (perm.Id == readPermission.Id) ? null : readPermission.Id;
                        if (perm.ParentId != targetParentId)
                        {
                            var parentNode = targetParentId.HasValue ? allPermissions.FirstOrDefault(p => p.Id == targetParentId.Value) : null;
                            perm.AssignParent(parentNode);
                            perm.RecalculateHierarchy();
                            hierarchyChanged = true;
                        }
                    }
                }
            }

            if (hierarchyChanged)
            {
                await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("[SystemRolePermissionSeed] 已成功完成 PermissionMetadata 階層化結構對齊。");
            }
        }

        private async Task AssignPermissionsToRoleAsync(string roleName, List<string> permissionCodes, CancellationToken cancellationToken)
        {
            var role = await _roleManager.FindByNameAsync(roleName).ConfigureAwait(false);
            if (role == null)
            {
                _logger.LogWarning("[SystemRolePermissionSeed] 找不到目標角色 [{RoleName}]，無法派發權限。", roleName);
                return;
            }

            string roleId = role.Id.ToString();

            var existingClaims = await _roleManager.GetClaimsAsync(role).ConfigureAwait(false);
            var existingPermissionSet = new HashSet<string>(
                existingClaims.Where(c => c.Type == ClaimTypePermission).Select(c => c.Value),
                StringComparer.OrdinalIgnoreCase
            );

            int addedCount = 0;
            foreach (var code in permissionCodes.Distinct())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!existingPermissionSet.Contains(code))
                {
                    var claim = new Claim(ClaimTypePermission, code);
                    var result = await _roleManager.AddClaimAsync(role, claim).ConfigureAwait(false);
                    if (result.Succeeded)
                    {
                        addedCount++;
                    }
                }
            }

            var targetPermissions = permissionCodes
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Dictionary<string, long> moduleBitmaskDict = await _bitmaskService
                .CalculateModuleBitmasksAsync(targetPermissions, cancellationToken)
                .ConfigureAwait(false);

            bool dbSuccess = await _rolePermissionRepository
                .SaveRoleGlobalPermissionsAsync(roleId, moduleBitmaskDict, cancellationToken)
                .ConfigureAwait(false);

            if (dbSuccess)
            {
                _logger.LogInformation("[SystemRolePermissionSeed] 角色 [{RoleName}] 權限同步完成：新增 Claims {AddedCount} 筆，更新 Role_Global_Permissions 模組數 {ModuleCount}。",
                    roleName, addedCount, moduleBitmaskDict.Count);
            }
        }

        private static ActionCategory DetermineDefaultCategory(string actionName, string permissionKey)
        {
            string permissionAction = string.Empty;
            if (!string.IsNullOrWhiteSpace(permissionKey))
            {
                var segments = permissionKey.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length > 0)
                {
                    permissionAction = segments[^1];
                }
            }

            if (permissionAction.Equals("ASSIGN", StringComparison.OrdinalIgnoreCase) ||
                permissionAction.Equals("GRANT", StringComparison.OrdinalIgnoreCase) ||
                actionName.Contains("Assign", StringComparison.OrdinalIgnoreCase) ||
                actionName.Contains("Grant", StringComparison.OrdinalIgnoreCase))
            {
                return ActionCategory.Administrative;
            }

            if (permissionAction.Equals("DELETE", StringComparison.OrdinalIgnoreCase) ||
                permissionAction.Equals("DEACTIVATE", StringComparison.OrdinalIgnoreCase) ||
                permissionAction.Equals("APPROVE", StringComparison.OrdinalIgnoreCase) ||
                permissionAction.Equals("REVOKE", StringComparison.OrdinalIgnoreCase) ||
                actionName.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
                actionName.Contains("Deactivate", StringComparison.OrdinalIgnoreCase) ||
                actionName.Contains("Approve", StringComparison.OrdinalIgnoreCase) ||
                actionName.Contains("Revoke", StringComparison.OrdinalIgnoreCase))
            {
                return ActionCategory.Critical;
            }

            if (permissionAction.Equals("CREATE", StringComparison.OrdinalIgnoreCase) ||
                permissionAction.Equals("EDIT", StringComparison.OrdinalIgnoreCase) ||
                permissionAction.Equals("UPDATE", StringComparison.OrdinalIgnoreCase) ||
                permissionAction.Equals("ACTIVATE", StringComparison.OrdinalIgnoreCase) ||
                permissionAction.Equals("DOWNLOAD", StringComparison.OrdinalIgnoreCase) ||
                actionName.Contains("Create", StringComparison.OrdinalIgnoreCase) ||
                actionName.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
                actionName.Contains("Edit", StringComparison.OrdinalIgnoreCase) ||
                actionName.Contains("Activate", StringComparison.OrdinalIgnoreCase) ||
                actionName.Contains("Set", StringComparison.OrdinalIgnoreCase) ||
                actionName.Contains("Move", StringComparison.OrdinalIgnoreCase))
            {
                return ActionCategory.Operational;
            }

            return ActionCategory.Basic;
        }
    }
}