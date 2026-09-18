// ==========================================
// 檔案路徑: src/SGSFramework/Infrastructure/SGSFramework.AuthTokenBucket/Services/PermissionSeedService.cs
// 架構層級: Infrastructure Layer
// ==========================================

namespace SGSFramework.AuthTokenBucket.Services
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using SGSFramework.AuthTokenBucket.Abstractions;
    using SGSFramework.Core.Abstractions.DbContexts;
    using SGSFramework.Core.Abstractions.Entities.Controller;
    using SGSFramework.Core.Abstractions.Permissions;
    using SGSFramework.Core.Abstractions.Permissions.Entities;
    using SGSFramework.Core.Abstractions.Permissions.Identities;
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
                    return;
                }

                var registeredPermissions = rawPermissions.ToList();

                var controllerMetadatas = await _dbContext.Set<ControllerMetadata>()
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
                        c.PermissionKey.Equals(key, StringComparison.OrdinalIgnoreCase));

                    string rawModuleName = !string.IsNullOrEmpty(matchedMeta?.ModuleName)
                        ? matchedMeta.ModuleName
                        : (!string.IsNullOrEmpty(perm.ModuleName) ? perm.ModuleName : "SGSFramework.System");

                    string fallbackModuleName = rawModuleName
                        .Replace("SGSFramework.", "", StringComparison.OrdinalIgnoreCase)
                        .Replace("PhysLIMS.", "", StringComparison.OrdinalIgnoreCase);

                    string moduleName = !string.IsNullOrEmpty(matchedMeta?.ModuleName)
                        ? matchedMeta.ModuleName
                        : (!string.IsNullOrEmpty(perm.ModuleName) ? perm.ModuleName : fallbackModuleName);

                    string moduleTitle = !string.IsNullOrEmpty(matchedMeta?.ModuleTitle)
                        ? matchedMeta.ModuleTitle
                        : (!string.IsNullOrEmpty(perm.ModuleTitle) ? perm.ModuleTitle : fallbackModuleName);

                    string controllerTitle = matchedMeta?.ControllerTitle ?? perm.ControllerTitle ?? controllerName;
                    string actionTitle = matchedMeta?.DisplayName ?? perm.ActionTitle ?? actionName;

                    // 核心修正：將 RequiresPermissionAttribute 帶入的 perm.PermissionTitle 優先級調至最高，若未指定則依據原規則判定
                    string defaultCalculatedTitle = (key.EndsWith(".READ", StringComparison.OrdinalIgnoreCase) || key.EndsWith("_READ", StringComparison.OrdinalIgnoreCase))
                        ? (!string.IsNullOrEmpty(matchedMeta?.ControllerTitle) ? matchedMeta.ControllerTitle : (perm.ControllerTitle ?? controllerTitle))
                        : (!string.IsNullOrEmpty(perm.ActionTitle) ? perm.ActionTitle : actionTitle);

                    string permissionTitle = !string.IsNullOrEmpty(perm.PermissionTitle)
                        ? perm.PermissionTitle
                        : defaultCalculatedTitle;

                    string description = !string.IsNullOrEmpty(matchedMeta?.Description)
                        ? matchedMeta.Description
                        : (perm.Description ?? $"Auto-scanned permission: {key}");

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
                        if (existingRecord.PermissionKey != key) { existingRecord.PermissionKey = key; isModified = true; }
                        if (existingRecord.BitPosition != bitPosition) { existingRecord.BitPosition = bitPosition; isModified = true; }
                        if (existingRecord.ModuleName != moduleName) { existingRecord.ModuleName = moduleName; isModified = true; }
                        if (existingRecord.ModuleTitle != moduleTitle) { existingRecord.ModuleTitle = moduleTitle; isModified = true; }
                        if (existingRecord.ControllerTitle != controllerTitle) { existingRecord.ControllerTitle = controllerTitle; isModified = true; }
                        if (existingRecord.ActionTitle != actionTitle) { existingRecord.ActionTitle = actionTitle; isModified = true; }
                        if (existingRecord.PermissionTitle != permissionTitle) { existingRecord.PermissionTitle = permissionTitle; isModified = true; }
                        if (string.IsNullOrEmpty(existingRecord.Description) || existingRecord.Description.StartsWith("Auto-scanned permission:"))
                        {
                            existingRecord.Description = description;
                            isModified = true;
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
                            ControllerTitle = controllerTitle,
                            ActionTitle = actionTitle,
                            PermissionTitle = permissionTitle,
                            ControllerName = controllerName,
                            ActionName = actionName,
                            Description = description
                        };

                        _dbContext.Set<PermissionMetadata>().Add(newPermission);
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

                foreach (var group in allPermissions.GroupBy(p => p.ControllerName))
                {
                    if (string.IsNullOrEmpty(group.Key)) continue;

                    var readPermission = group.FirstOrDefault(p => p.PermissionKey.EndsWith(".READ", StringComparison.OrdinalIgnoreCase) || p.PermissionKey.EndsWith("_READ", StringComparison.OrdinalIgnoreCase))
                                      ?? group.FirstOrDefault();

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
                    _logger.LogInformation("已完整同步系統所有 Action 及其覆寫後的 PermissionTitle 至資料庫。");
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