// ==========================================
// 檔案路徑: src/SGSFramework/Infrastructure/SGSFramework.AuthTokenBucket/Services/PermissionSeedService.cs
// 架構層級: Infrastructure Layer
// ==========================================

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

                    // 強化對應邏輯：優先以 ControllerName + ActionName 匹配，次之以 PermissionKey，最後以 ControllerName 進行群組對齊，確保模組與 ControllerMetadata 100% 一致
                    var matchedMeta = controllerMetadatas.FirstOrDefault(c =>
                        c.ControllerName.Equals(controllerName, StringComparison.OrdinalIgnoreCase) &&
                        c.ActionName.Equals(actionName, StringComparison.OrdinalIgnoreCase))
                        ?? controllerMetadatas.FirstOrDefault(c =>
                        c.PermissionKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                        ?? controllerMetadatas.FirstOrDefault(c =>
                        c.ControllerName.Equals(controllerName, StringComparison.OrdinalIgnoreCase));

                    // 嚴格對齊 ControllerMetadata 的 ModuleName 與 ModuleTitle
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

                    // 處理 BitPosition 衝突防範
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
                            Description = description
                        };

                        await _dbContext.Set<PermissionMetadata>().AddAsync(newPermission, cancellationToken).ConfigureAwait(false);
                        isModified = true;
                    }

                    if (isModified)
                    {
                        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

                // 階層化同步處理 (建構 Parent-Child 關聯與計算 Level/NodePath)
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
                    _logger.LogInformation("已成功完成 PermissionMetadata 與 ControllerMetadata 之模組與階層化結構完整對齊同步。");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "同步與種子化權限資料時發生未預期異常。");
                throw;
            }
        }
    }
}