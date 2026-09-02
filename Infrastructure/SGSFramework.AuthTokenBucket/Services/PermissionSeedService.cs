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

    /// <summary>
    /// PermissionSeedService 類別負責直接承接 IPermissionRegistry 的完整解析與掃描結果，
    /// 並結合 ControllerMetadata 同步模組中文標題、控制器標題、Action中文標題與階層路徑（IHierarchicalEntity），專注執行資料庫的持久化同步作業。
    /// </summary>
    /// <typeparam name="TDbContext">資料庫上下文型別，需實作 ITokenDbContext</typeparam>
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

        /// <summary>
        /// 執行權限資料的自動同步與種子化操作，將 DynamicPermissionRegistry 中的權限與 ControllerMetadata 進行比對、階層計算與更新。
        /// </summary>
        public async Task SeedAndSyncPermissionsAsync(CancellationToken cancellationToken = default)
        {
            var registeredPermissions = _permissionRegistry.GetAllPermissions();
            if (registeredPermissions == null || registeredPermissions.Count == 0)
            {
                return;
            }

            var controllerMetadatas = await _dbContext.Set<ControllerMetadata>()
                .ToListAsync(cancellationToken);

            foreach (var perm in registeredPermissions)
            {
                string key = perm.PermissionKey;
                int bitPosition = perm.BitPosition;

                var matchedMeta = controllerMetadatas.FirstOrDefault(c =>
                    c.PermissionKey.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                    (c.ControllerName.Equals(perm.ControllerName, StringComparison.OrdinalIgnoreCase) &&
                     c.ActionName.Equals(perm.ActionName, StringComparison.OrdinalIgnoreCase)));

                string moduleName = !string.IsNullOrEmpty(perm.ModuleName) ? perm.ModuleName : "SGSFramework.System";
                string moduleTitle = matchedMeta?.ModuleTitle ?? perm.ModuleTitle ?? moduleName;
                string controllerTitle = matchedMeta?.ControllerTitle ?? perm.ControllerTitle ?? perm.ControllerName;
                string actionTitle = matchedMeta?.DisplayName ?? perm.ActionTitle ?? perm.ActionName;
                string controllerName = perm.ControllerName ?? string.Empty;
                string actionName = perm.ActionName ?? string.Empty;
                string description = !string.IsNullOrEmpty(matchedMeta?.Description)
                    ? matchedMeta.Description
                    : (perm.Description ?? $"Auto-scanned permission: {key}");

                // 1. 檢查 BitPosition 唯一性衝突
                var conflictByBit = await _dbContext.Set<PermissionMetadata>()
                    .FirstOrDefaultAsync(p => p.BitPosition == bitPosition && p.PermissionKey != key, cancellationToken);

                if (conflictByBit != null)
                {
                    conflictByBit.BitPosition = -Math.Abs(conflictByBit.Id + 10000);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                // 2. 查詢現有記錄
                var existingByKey = await _dbContext.Set<PermissionMetadata>()
                    .FirstOrDefaultAsync(p => p.PermissionKey == key, cancellationToken);

                bool isModified = false;

                if (existingByKey != null)
                {
                    if (existingByKey.BitPosition != bitPosition) { existingByKey.BitPosition = bitPosition; isModified = true; }
                    if (existingByKey.ModuleName != moduleName) { existingByKey.ModuleName = moduleName; isModified = true; }
                    if (existingByKey.ModuleTitle != moduleTitle) { existingByKey.ModuleTitle = moduleTitle; isModified = true; }
                    if (existingByKey.ControllerTitle != controllerTitle) { existingByKey.ControllerTitle = controllerTitle; isModified = true; }
                    if (existingByKey.ActionTitle != actionTitle) { existingByKey.ActionTitle = actionTitle; isModified = true; }
                    if (existingByKey.ControllerName != controllerName) { existingByKey.ControllerName = controllerName; isModified = true; }
                    if (existingByKey.ActionName != actionName) { existingByKey.ActionName = actionName; isModified = true; }
                    if (string.IsNullOrEmpty(existingByKey.Description) || existingByKey.Description.StartsWith("Auto-scanned permission:"))
                    {
                        existingByKey.Description = description;
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
                        ControllerName = controllerName,
                        ActionName = actionName,
                        Description = description
                    };

                    _dbContext.Set<PermissionMetadata>().Add(newPermission);
                    isModified = true;
                }

                if (isModified)
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            // ==========================================
            // 3. 自動建立階層關聯：以同一 Controller 且尾綴為 _READ 者作為父節點
            // ==========================================
            var allPermissions = await _dbContext.Set<PermissionMetadata>().ToListAsync(cancellationToken);
            bool hierarchyChanged = false;

            foreach (var group in allPermissions.GroupBy(p => p.ControllerName))
            {
                if (string.IsNullOrEmpty(group.Key)) continue;

                var readPermission = group.FirstOrDefault(p => p.PermissionKey.EndsWith("_READ", StringComparison.OrdinalIgnoreCase))
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
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("已自動依據 Controller 與 _READ 尾綴更新權限階層樹狀結構。");
            }
        }
    }
}