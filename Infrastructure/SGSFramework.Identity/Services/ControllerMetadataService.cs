using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Identities;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.Services
{
    /// <summary>
    /// 系統控制器中繼資料服務實作（支援權限綁定樹狀與平坦清單查詢）
    /// </summary>
    public class ControllerMetadataService(
        DbContext dbContext,
        IMemoryCache cache,
        ICurrentUserService currentUserService,
        IPermissionChangeNotifier permissionChangeNotifier,
        IModuleStatusReader moduleStatusReader,
        TimeProvider timeProvider,
        IMenuSeedService menuSeedService,
        ILogger<ControllerMetadataService> logger) : IControllerMetadataService
    {
        /// <summary>SignalR 廣播原因代碼，前端據此重新載入選單與系統公告</summary>
        public const string FunctionStatusChangedReason = "function-status-changed";

        private readonly DbContext _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        private readonly IMemoryCache _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        private readonly ICurrentUserService _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        private readonly IPermissionChangeNotifier _permissionChangeNotifier = permissionChangeNotifier ?? throw new ArgumentNullException(nameof(permissionChangeNotifier));
        private readonly IModuleStatusReader _moduleStatusReader = moduleStatusReader ?? throw new ArgumentNullException(nameof(moduleStatusReader));
        private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        private readonly IMenuSeedService _menuSeedService = menuSeedService ?? throw new ArgumentNullException(nameof(menuSeedService));
        private readonly ILogger<ControllerMetadataService> _logger =logger ?? throw new ArgumentNullException(nameof(logger));

        public async Task<IEnumerable<ControllerMetadataDto>> GetAllControllerMetadatasAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
        {
            try
            {
                var inactiveModules = await _moduleStatusReader.GetInactiveModuleNamesAsync(cancellationToken);

                // 分組覆寫的預設標題需參照全部中繼資料（含停用），再依 includeInactive 篩選輸出
                var entities = await _dbContext.Set<ControllerMetadata>()
                    .AsNoTracking()
                    .OrderBy(m => m.DisplayOrder)
                    .ToListAsync(cancellationToken);

                var overrides = await _dbContext.Set<FunctionGroupOverride>()
                    .AsNoTracking()
                    .ToListAsync(cancellationToken);

                var resolver = new FunctionGroupResolver(entities, overrides);
                var list = entities
                    .Where(m => includeInactive || m.IsActive)
                    .Select(m => ApplyGroup(ToDto(m), m, resolver))
                    .ToList();

                // 模組停用僅記錄於 ModuleMetadata，於此標示所屬功能視同停用
                foreach (var item in list.Where(i => inactiveModules.Contains(i.ModuleName)))
                {
                    item.IsModuleActive = false;
                }

                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得系統控制器中繼資料清單時發生未預期異常。");
                throw;
            }
        }

        public async Task<ControllerMetadataDto> UpdateFunctionStatusAsync(Guid id, FunctionStatusChange change, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(change);

            if (id == Guid.Empty)
            {
                throw new ArgumentException("識別碼不可為空 Guid。", nameof(id));
            }

            var reason = Normalize(change.Reason);
            var notice = Normalize(change.Notice);
            var now = _timeProvider.GetUtcNow();

            // 停用必須說明原因，作為資安稽核依據（前端亦有檢核，此處為伺服器端強制）
            if (!change.IsActive && reason is null)
            {
                throw new ArgumentException("停用功能時必須填寫停用原因。", nameof(change));
            }

            if (!change.IsActive && change.ExpectedRestoreAt is { } restoreAt && restoreAt <= now)
            {
                throw new ArgumentException("預計恢復時間必須晚於目前時間。", nameof(change));
            }

            try
            {
                var entity = await _dbContext.Set<ControllerMetadata>()
                    .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

                if (entity == null)
                {
                    throw new KeyNotFoundException($"找不到識別碼為 '{id}' 的功能中繼資料。");
                }

                if (!change.IsActive && FunctionStatusRules.IsProtected(entity.ControllerName))
                {
                    throw new InvalidOperationException("「系統功能管理」本身的功能不可停用，以免無法再恢復其他功能的狀態。");
                }

                // 已停用狀態下再次送出停用，視為更新公告內容 / 預計恢復時間，仍需留下稽核紀錄
                bool statusUnchanged = entity.IsActive == change.IsActive && entity.IsManuallyDisabled == !change.IsActive;
                if (statusUnchanged && change.IsActive)
                {
                    _logger.LogInformation("功能識別碼 '{Id}' 已為啟用狀態，無須重複更新。", id);
                    return ToDto(entity);
                }

                entity.IsActive = change.IsActive;
                // 記錄人工決策，避免 API 重啟時的中繼資料同步將停用的功能恢復為啟用
                entity.IsManuallyDisabled = !change.IsActive;

                if (change.IsActive)
                {
                    entity.DisabledNotice = null;
                    entity.DisabledAt = null;
                    entity.ExpectedRestoreAt = null;
                }
                else
                {
                    entity.DisabledNotice = notice ?? $"「{entity.DisplayName}」功能暫停使用，造成不便敬請見諒。";
                    entity.DisabledAt = statusUnchanged ? entity.DisabledAt ?? now : now;
                    entity.ExpectedRestoreAt = change.ExpectedRestoreAt;
                }

                _dbContext.Set<FunctionStatusLog>().Add(new FunctionStatusLog
                {
                    ControllerMetadataId = entity.Id,
                    ControllerName = entity.ControllerName,
                    ActionName = entity.ActionName,
                    DisplayName = entity.DisplayName,
                    PermissionKey = entity.PermissionKey,
                    IsActive = change.IsActive,
                    Reason = reason,
                    Notice = entity.DisabledNotice,
                    ExpectedRestoreAt = entity.ExpectedRestoreAt,
                    ActorUserId = Guid.TryParse(_currentUserService.UserId, out var actorId) ? actorId : null,
                    ActorName = _currentUserService.UserName,
                    ClientIp = change.ClientIp,
                    CreatedAt = now
                });

                await _dbContext.SaveChangesAsync(cancellationToken);

                // 清除 FunctionStatusAuthorizationFilter 的停用清單快取，使變更立即生效
                _cache.Remove(FunctionStatusRules.DisabledFunctionsCacheKey);

                // 即時通知所有已連線使用者重新載入選單與系統公告
                await _permissionChangeNotifier.BroadcastAsync(FunctionStatusChangedReason, cancellationToken);

                _logger.LogWarning("功能狀態變更成功。識別碼: {Id}, 功能: {Function}, 新狀態: {IsActive}, 操作者: {Actor}, 來源 IP: {ClientIp}, 變更原因: {Reason}",
                    id, $"{entity.ControllerName}.{entity.ActionName}", change.IsActive, _currentUserService.UserName ?? "Unknown", change.ClientIp, reason ?? "未提供");

                return ToDto(entity);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("更新功能狀態之操作已被取消。識別碼: {Id}", id);
                throw;
            }
            catch (Exception ex) when (ex is not KeyNotFoundException && ex is not ArgumentException && ex is not InvalidOperationException)
            {
                _logger.LogError(ex, "更新功能狀態時發生資料庫異動異常。識別碼: {Id}", id);
                throw;
            }
        }

        public async Task<ControllerMetadataDto> UpdateFunctionMetadataAsync(Guid id, FunctionMetadataChange change, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(change);

            if (id == Guid.Empty)
            {
                throw new ArgumentException("識別碼不可為空 Guid。", nameof(id));
            }

            var displayName = Normalize(change.DisplayName)
                ?? throw new ArgumentException("功能名稱不可為空白。", nameof(change));

            if (displayName.Length > 100)
            {
                throw new ArgumentException("功能名稱長度不可超過 100 字。", nameof(change));
            }

            var icon = Normalize(change.Icon);
            if (icon is { Length: > 100 })
            {
                throw new ArgumentException("圖示長度不可超過 100 字。", nameof(change));
            }

            var entity = await _dbContext.Set<ControllerMetadata>()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (entity == null)
            {
                throw new KeyNotFoundException($"找不到識別碼為 '{id}' 的功能中繼資料。");
            }

            // 系統功能管理本身的選單入口不可移出選單，避免管理者找不到入口而無法再調整設定
            if (entity.IsMenu && !change.IsMenu && FunctionStatusRules.IsProtected(entity.ControllerName))
            {
                throw new InvalidOperationException("「系統功能管理」本身的選單入口不可移出選單。");
            }

            bool menuChanged = entity.IsMenu != change.IsMenu;
            var before = $"名稱={entity.DisplayName}; 說明={entity.Description}; 排序={entity.DisplayOrder}; 圖示={entity.Icon}; IsMenu={entity.IsMenu}";

            entity.DisplayName = displayName;
            entity.Description = Normalize(change.Description);
            entity.DisplayOrder = change.DisplayOrder;
            entity.Icon = icon;
            entity.IsMenu = change.IsMenu;
            // 標記為已自訂：啟動同步不再以程式碼 Attribute 覆蓋這些欄位
            entity.IsCustomized = true;

            await _dbContext.SaveChangesAsync(cancellationToken);
            InvalidatePermissionTree();

            // IsMenu 有變動：立即建立（或恢復）/ 隱藏該功能的選單頁面節點，必須在中繼資料存檔之後執行
            // IsMenu 開啟時每次儲存都同步（冪等）：可補建先前漏建的節點
            if (menuChanged || change.IsMenu)
            {
                await _menuSeedService.SyncFunctionMenuAsync(entity.Id, cancellationToken);
            }

            // 通知所有連線使用者重新載入選單與公告
            await _permissionChangeNotifier.BroadcastAsync(FunctionStatusChangedReason, cancellationToken);

            _logger.LogWarning("功能顯示欄位已編輯。識別碼: {Id}, 功能: {Function}, 變更前: {Before}, 變更後: 名稱={DisplayName}; 說明={Description}; 排序={Order}; 圖示={Icon}; IsMenu={IsMenu}, 操作者: {Actor}, 來源 IP: {ClientIp}",
                id, $"{entity.ControllerName}.{entity.ActionName}", before,
                entity.DisplayName, entity.Description, entity.DisplayOrder, entity.Icon, entity.IsMenu,
                _currentUserService.UserName ?? "Unknown", change.ClientIp);

            return ToDto(entity);
        }

        public async Task<IReadOnlyList<FunctionStatusLogDto>> GetFunctionStatusLogsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Set<FunctionStatusLog>()
                .AsNoTracking()
                .Where(l => l.ControllerMetadataId == id)
                .OrderByDescending(l => l.CreatedAt)
                .Take(200)
                .Select(l => new FunctionStatusLogDto
                {
                    Id = l.Id,
                    IsActive = l.IsActive,
                    Reason = l.Reason,
                    Notice = l.Notice,
                    ExpectedRestoreAt = l.ExpectedRestoreAt,
                    ActorName = l.ActorName,
                    ClientIp = l.ClientIp,
                    CreatedAt = l.CreatedAt
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<FunctionNoticeDto>> GetActiveNoticesAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.Set<ControllerMetadata>()
                .AsNoTracking()
                .Where(m => m.IsManuallyDisabled)
                .OrderByDescending(m => m.DisabledAt)
                .Select(m => new FunctionNoticeDto
                {
                    Id = m.Id,
                    ModuleTitle = m.ModuleTitle,
                    ControllerTitle = m.ControllerTitle,
                    DisplayName = m.DisplayName,
                    Notice = m.DisabledNotice ?? string.Empty,
                    DisabledAt = m.DisabledAt,
                    ExpectedRestoreAt = m.ExpectedRestoreAt
                })
                .ToListAsync(cancellationToken);
        }

        public async Task UpdateModuleGroupAsync(string moduleName, ModuleGroupChange change, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(change);

            var module = Normalize(moduleName) ?? throw new ArgumentException("模組名稱不可為空白。", nameof(moduleName));
            var title = Normalize(change.Title);
            if (title is { Length: > 100 })
            {
                throw new ArgumentException("模組名稱長度不可超過 100 字。", nameof(change));
            }

            if (!await _dbContext.Set<ControllerMetadata>().AnyAsync(m => m.ModuleName == module, cancellationToken))
            {
                throw new KeyNotFoundException($"找不到模組 '{module}'。");
            }

            var entity = await UpsertGroupOverrideAsync(FunctionGroupLevel.Module, module, string.Empty, cancellationToken);
            var before = DescribeOverride(entity);

            entity.Title = title;
            entity.DisplayOrder = change.DisplayOrder;

            await SaveGroupOverrideAsync(entity, cancellationToken);

            _logger.LogWarning("功能管理模組顯示欄位已編輯。模組: {Module}, 變更前: {Before}, 變更後: {After}, 操作者: {Actor}, 來源 IP: {ClientIp}",
                module, before, DescribeOverride(entity), _currentUserService.UserName ?? "Unknown", change.ClientIp);
        }

        public async Task UpdateControllerGroupAsync(string moduleName, string controllerName, ControllerGroupChange change, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(change);

            var module = Normalize(moduleName) ?? throw new ArgumentException("模組名稱不可為空白。", nameof(moduleName));
            var controller = Normalize(controllerName) ?? throw new ArgumentException("Controller 名稱不可為空白。", nameof(controllerName));
            var title = Normalize(change.Title);
            var icon = Normalize(change.Icon);
            if (title is { Length: > 100 })
            {
                throw new ArgumentException("Controller 名稱長度不可超過 100 字。", nameof(change));
            }
            if (icon is { Length: > 100 })
            {
                throw new ArgumentException("圖示長度不可超過 100 字。", nameof(change));
            }

            if (!await _dbContext.Set<ControllerMetadata>().AnyAsync(m => m.ModuleName == module && m.ControllerName == controller, cancellationToken))
            {
                throw new KeyNotFoundException($"找不到 Controller '{module} / {controller}'。");
            }

            // 改掛回原模組視同還原
            var targetModule = Normalize(change.TargetModuleName);
            if (targetModule is not null && string.Equals(targetModule, module, StringComparison.OrdinalIgnoreCase))
            {
                targetModule = null;
            }
            if (targetModule is not null && !await _dbContext.Set<ControllerMetadata>().AnyAsync(m => m.ModuleName == targetModule, cancellationToken))
            {
                throw new ArgumentException($"找不到目標模組 '{targetModule}'。", nameof(change));
            }

            var entity = await UpsertGroupOverrideAsync(FunctionGroupLevel.Controller, module, controller, cancellationToken);
            var before = DescribeOverride(entity);

            entity.Title = title;
            entity.Icon = icon;
            entity.DisplayOrder = change.DisplayOrder;
            entity.TargetModuleName = targetModule;

            await SaveGroupOverrideAsync(entity, cancellationToken);

            _logger.LogWarning("功能管理 Controller 顯示欄位已編輯。Controller: {Controller}, 變更前: {Before}, 變更後: {After}, 操作者: {Actor}, 來源 IP: {ClientIp}",
                $"{module}/{controller}", before, DescribeOverride(entity), _currentUserService.UserName ?? "Unknown", change.ClientIp);
        }

        public async Task UpdateFunctionGroupAsync(Guid id, FunctionGroupChange change, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(change);

            if (id == Guid.Empty)
            {
                throw new ArgumentException("識別碼不可為空 Guid。", nameof(id));
            }

            var entity = await _dbContext.Set<ControllerMetadata>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? throw new KeyNotFoundException($"找不到識別碼為 '{id}' 的功能中繼資料。");

            var targetModule = Normalize(change.TargetModuleName);
            var targetController = Normalize(change.TargetControllerName);

            if (targetController is not null)
            {
                if (targetModule is null)
                {
                    throw new ArgumentException("改掛時必須指定目標 Controller 所屬的模組。", nameof(change));
                }

                if (!await _dbContext.Set<ControllerMetadata>().AnyAsync(m => m.ModuleName == targetModule && m.ControllerName == targetController, cancellationToken))
                {
                    throw new ArgumentException($"找不到目標 Controller '{targetModule} / {targetController}'。", nameof(change));
                }

                // 改掛回原 Controller 視同還原
                if (string.Equals(targetModule, entity.ModuleName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(targetController, entity.ControllerName, StringComparison.OrdinalIgnoreCase))
                {
                    targetModule = null;
                    targetController = null;
                }
            }
            else
            {
                targetModule = null;
            }

            var before = entity.GroupControllerName is null ? "原 Controller" : $"{entity.GroupModuleName}/{entity.GroupControllerName}";

            entity.GroupModuleName = targetModule;
            entity.GroupControllerName = targetController;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogWarning("功能顯示用改掛已變更。功能: {Function}, 變更前: {Before}, 變更後: {After}, 操作者: {Actor}, 來源 IP: {ClientIp}",
                $"{entity.ControllerName}.{entity.ActionName}", before,
                targetController is null ? "原 Controller" : $"{targetModule}/{targetController}",
                _currentUserService.UserName ?? "Unknown", change.ClientIp);
        }

        private async Task<FunctionGroupOverride> UpsertGroupOverrideAsync(FunctionGroupLevel level, string moduleName, string controllerName, CancellationToken cancellationToken)
        {
            var entity = await _dbContext.Set<FunctionGroupOverride>()
                .FirstOrDefaultAsync(o => o.Level == level && o.ModuleName == moduleName && o.ControllerName == controllerName, cancellationToken);

            if (entity is null)
            {
                entity = new FunctionGroupOverride { Level = level, ModuleName = moduleName, ControllerName = controllerName };
                _dbContext.Set<FunctionGroupOverride>().Add(entity);
            }

            return entity;
        }

        /// <summary>全部欄位皆還原為預設時刪除覆寫列，避免殘留空白紀錄</summary>
        private async Task SaveGroupOverrideAsync(FunctionGroupOverride entity, CancellationToken cancellationToken)
        {
            if (entity is { Title: null, Icon: null, DisplayOrder: null, TargetModuleName: null })
            {
                if (_dbContext.Entry(entity).State == EntityState.Added)
                {
                    _dbContext.Entry(entity).State = EntityState.Detached;
                }
                else
                {
                    _dbContext.Set<FunctionGroupOverride>().Remove(entity);
                }
            }
            else
            {
                entity.UpdatedAt = _timeProvider.GetUtcNow();
                entity.UpdatedBy = _currentUserService.UserName;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            InvalidatePermissionTree();
        }

        private static string DescribeOverride(FunctionGroupOverride entity) =>
            $"名稱={entity.Title ?? "(預設)"}; 圖示={entity.Icon ?? "(預設)"}; 排序={entity.DisplayOrder?.ToString() ?? "(預設)"}; 父模組={entity.TargetModuleName ?? "(原模組)"}";

        /// <summary>
        /// 將模組 / Controller 覆寫與功能改掛套用到 DTO 的標題與顯示分組欄位
        /// </summary>
        private static ControllerMetadataDto ApplyGroup(ControllerMetadataDto dto, ControllerMetadata entity, FunctionGroupResolver resolver)
        {
            dto.ModuleTitle = resolver.ModuleTitle(entity.ModuleName);
            dto.ControllerTitle = resolver.ControllerTitle(entity.ModuleName, entity.ControllerName);

            var placement = resolver.Resolve(entity);
            dto.GroupControllerModuleName = placement.ControllerModuleName;
            dto.GroupControllerName = placement.ControllerName;
            dto.GroupControllerTitle = resolver.ControllerTitle(placement.ControllerModuleName, placement.ControllerName);
            dto.GroupControllerIcon = resolver.ControllerIcon(placement.ControllerModuleName, placement.ControllerName);
            dto.GroupControllerOrder = resolver.ControllerOrder(placement.ControllerModuleName, placement.ControllerName);

            dto.GroupModuleName = placement.ModuleName;
            dto.GroupModuleTitle = resolver.ModuleTitle(placement.ModuleName);
            dto.GroupModuleOrder = resolver.ModuleOrder(placement.ModuleName);

            dto.IsRelocated = placement.IsRelocated;
            dto.IsControllerRelocated = placement.IsControllerRelocated;
            return dto;
        }

        /// <summary>系統權限清單的名稱取自 ControllerMetadata：改名 / 編輯功能後清除其快取，使變更立即反映</summary>
        private void InvalidatePermissionTree() => _cache.Remove(FunctionStatusRules.PermissionTreeCacheKey);

        private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static ControllerMetadataDto ToDto(ControllerMetadata entity) => new()
        {
            Id = entity.Id,
            Version = entity.Version,
            ControllerTypeName = entity.ControllerTypeName,
            ModuleName = entity.ModuleName,
            ModuleTitle = entity.ModuleTitle,
            ControllerTitle = entity.ControllerTitle,
            ControllerName = entity.ControllerName,
            ActionName = entity.ActionName,
            DisplayName = entity.DisplayName,
            PermissionKey = entity.PermissionKey,
            RouteTemplate = entity.RouteTemplate,
            Description = entity.Description,
            Icon = entity.Icon,
            DisplayOrder = entity.DisplayOrder,
            ParentMenuName = entity.ParentMenuName,
            IsMenu = entity.IsMenu,
            IsActive = entity.IsActive,
            IsManuallyDisabled = entity.IsManuallyDisabled,
            IsCustomized = entity.IsCustomized,
            DisabledNotice = entity.DisabledNotice,
            DisabledAt = entity.DisabledAt,
            ExpectedRestoreAt = entity.ExpectedRestoreAt
        };


        public async Task<IEnumerable<ModulePermissionGroupDto>> GetPermissionTreeAsync(CancellationToken cancellationToken = default)
        {
            var flatList = await GetAllControllerMetadatasAsync(cancellationToken: cancellationToken);

            // 於記憶體中進行階層群組轉換 (ModuleName -> ControllerName -> Actions)
            var tree = flatList
                .GroupBy(m => new { m.ModuleName, m.ModuleTitle })
                .Select(moduleGroup => new ModulePermissionGroupDto
                {
                    ModuleName = moduleGroup.Key.ModuleName,
                    ModuleTitle = moduleGroup.Key.ModuleTitle,
                    Controllers = moduleGroup
                        .GroupBy(c => new { c.ControllerName, c.ControllerTitle, c.Icon, c.DisplayOrder })
                        .Select(controllerGroup => new ControllerPermissionGroupDto
                        {
                            ControllerName = controllerGroup.Key.ControllerName,
                            ControllerTitle = controllerGroup.Key.ControllerTitle,
                            Icon = controllerGroup.Key.Icon,
                            DisplayOrder = controllerGroup.Key.DisplayOrder,
                            Actions = controllerGroup.OrderBy(a => a.DisplayOrder).ToList()
                        })
                        .OrderBy(c => c.DisplayOrder)
                        .ToList()
                })
                .ToList();

            return tree;
        }



    }
}
