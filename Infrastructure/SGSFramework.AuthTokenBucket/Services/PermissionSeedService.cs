#nullable enable

namespace SGSFramework.AuthTokenBucket.Services
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using SGSFramework.AuthTokenBucket.Abstractions;
    using SGSFramework.Core.Abstractions.DbContexts;
    using SGSFramework.Core.Abstractions.Entities.Controller;
    using SGSFramework.Core.Abstractions.Permissions;
    using SGSFramework.Core.Abstractions.Permissions.Entities;
    using SGSFramework.Core.Abstractions.Permissions.Enums;
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class PermissionSeedService<TDbContext> : IPermissionSeedService
        where TDbContext : DbContext, ITokenDbContext
    {
        private readonly TDbContext _dbContext;
        private readonly IPermissionRegistry _permissionRegistry;
        private readonly ILogger<PermissionSeedService<TDbContext>> _logger;

        public PermissionSeedService(
            TDbContext dbContext,
            IPermissionRegistry permissionRegistry,
            ILogger<PermissionSeedService<TDbContext>> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _permissionRegistry = permissionRegistry ?? throw new ArgumentNullException(nameof(permissionRegistry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SeedAndSyncPermissionsAsync(CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(_dbContext);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rawPermissions = _permissionRegistry.GetAllPermissions();
                if (rawPermissions == null || rawPermissions.Count == 0)
                {
                    _logger.LogWarning("從權限註冊表中未取得任何權限定義，略過同步。");
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


                    // 強制優先透過名稱與 ActionKey 進行智慧推導，若開發者有明確透過 Attribute 指定再覆蓋
                    ActionCategory category = DetermineDefaultCategory(actionName, key);
                    if (perm.Category != default(ActionCategory) && perm.Category != ActionCategory.Basic)
                    {
                        category = perm.Category;
                    }

                    var conflictByBit = await _dbContext.Set<PermissionMetadata>()
                        .FirstOrDefaultAsync(p => p.BitPosition == bitPosition && !(p.ControllerName == controllerName && p.ActionName == actionName), cancellationToken)
                        .ConfigureAwait(false);

                    if (conflictByBit != null)
                    {
                        conflictByBit.BitPosition = -Math.Abs(conflictByBit.Id + 10000);
                        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

                        // 同步 Category 欄位
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
                            Category = category // 寫入 ActionCategory
                        };

                        await _dbContext.Set<PermissionMetadata>().AddAsync(newPermission, cancellationToken).ConfigureAwait(false);
                        isModified = true;
                    }

                    if (isModified)
                    {
                        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

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
                    _logger.LogInformation("已成功完成 PermissionMetadata (含 Category) 與 ControllerMetadata 之模組與階層化結構完整對齊同步。");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步與種子化權限資料時發生未預期異常。");
                throw;
            }
        }

        /// <summary>
        /// 根據 PermissionKey 的最後一段動作與 ActionName 智能判斷預設的權限敏感度級別
        /// </summary>
        private static ActionCategory DetermineDefaultCategory(string actionName, string permissionKey)
        {
            // 1. 安全解析 PermissionKey 的最後一段作為動作動詞 (例如: "ORG.LABORATORY.DELETE" -> "DELETE")
            string permissionAction = string.Empty;
            if (!string.IsNullOrWhiteSpace(permissionKey))
            {
                var segments = permissionKey.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 3)
                {
                    permissionAction = segments[^1]; // 取得最後一段
                }
                else if (segments.Length > 0)
                {
                    permissionAction = segments[^1];
                }
            }

            // 2. 判斷高權限 / 系統管理操作 (Administrative)
            if (permissionAction.Equals("ASSIGN", StringComparison.OrdinalIgnoreCase) ||
                permissionAction.Equals("GRANT", StringComparison.OrdinalIgnoreCase) ||
                actionName.Contains("Assign", StringComparison.OrdinalIgnoreCase) ||
                actionName.Contains("Grant", StringComparison.OrdinalIgnoreCase))
            {
                return ActionCategory.Administrative;
            }

            // 3. 判斷高風險 / 破壞性操作 (Critical)
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

            // 4. 判斷一般異動/業務操作 (Operational)
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

            // 5. 其餘預設為基本讀取操作 (Basic)，例如 READ, LIST, GET 等
            return ActionCategory.Basic;
        }
    }
}