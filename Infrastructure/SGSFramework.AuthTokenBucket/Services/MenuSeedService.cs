using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SGSFramework.AuthTokenBucket.Services;

/// <summary>
/// 動態選單自動掃描與資料庫種子同步服務
/// 專注於將包含 IsMenu=true 標記之 API 自動轉換為選單階層結構：
/// Label(領域類別，取自 [assembly: Module] 的 Domain) -> Section(模組，[Module] 的 Title / Icon / Order)
/// -> Group(區塊，[ControllerTitle]) -> Page(頁面，[Function] IsMenu = true)。
/// IsMenu = false 的 [Function] 為頁面下的「操作」權限，不產生選單節點。
/// </summary>
/// <typeparam name="TDbContext">資料庫上下文型別，需實作 ITokenDbContext</typeparam>
public class MenuSeedService<TDbContext> : IMenuSeedService
    where TDbContext : DbContext, ITokenDbContext
{
    /// <summary>未宣告 Domain 的模組所歸屬的預設領域類別</summary>
    private const string DefaultDomain = "其他領域類";
    private const int DefaultDomainOrder = 99;

    private readonly TDbContext _dbContext;
    private readonly ILogger<MenuSeedService<TDbContext>> _logger;
    private readonly TimeProvider _timeProvider;

    public MenuSeedService(
        TDbContext dbContext,
        ILogger<MenuSeedService<TDbContext>> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// 確保「領域類別」標題節點存在（Key = LBL_{領域類別名稱}，位於最上層）。
    /// 既有節點不覆寫，保留管理者在選單維護的調整。
    /// </summary>
    private MenuItem EnsureLabel(List<MenuItem> items, string? domain, int domainOrder, ref bool isModified)
    {
        string title = string.IsNullOrWhiteSpace(domain) ? DefaultDomain : domain.Trim();
        int order = string.IsNullOrWhiteSpace(domain) ? DefaultDomainOrder : domainOrder;
        string key = $"LBL_{title}";

        var label = items.FirstOrDefault(m => m.Key == key);
        if (label != null) return label;

        label = new MenuItem
        {
            Id = Guid.NewGuid(),
            Key = key,
            DisplayName = title,
            Icon = "fa-solid fa-folder",
            DisplayOrder = order,
            Type = MenuType.Label,
            ModuleName = "System",
            IsActive = true,
            IsVisible = true
        };
        _dbContext.Set<MenuItem>().Add(label);
        items.Add(label);
        isModified = true;
        return label;
    }

    /// <summary>
    /// 確保模組 Section 節點存在並掛在領域類別之下。
    /// 新建時圖示 / 排序取自 [Module]；既有節點不覆寫名稱與排序，僅在尚未有父節點（舊結構的頂層區塊）時補掛到領域類別。
    /// </summary>
    private MenuItem EnsureModuleSection(
        List<MenuItem> items,
        string moduleName,
        string? moduleTitle,
        string? domain,
        int domainOrder,
        string? moduleIcon,
        int moduleOrder,
        ref bool isModified)
    {
        string sectionKey = $"SEC_{moduleName.ToUpperInvariant()}";
        var section = items.FirstOrDefault(m => m.Key == sectionKey);

        if (section == null)
        {
            var label = EnsureLabel(items, domain, domainOrder, ref isModified);
            section = new MenuItem
            {
                Id = Guid.NewGuid(),
                ParentId = label.Id,
                Key = sectionKey,
                DisplayName = string.IsNullOrWhiteSpace(moduleTitle) ? moduleName : moduleTitle,
                Icon = string.IsNullOrWhiteSpace(moduleIcon) ? "fa-solid fa-folder" : moduleIcon,
                DisplayOrder = moduleOrder,
                Type = MenuType.Section,
                ModuleName = moduleName,
                IsActive = true,
                IsVisible = true
            };
            _dbContext.Set<MenuItem>().Add(section);
            items.Add(section);
            isModified = true;
        }
        else if (section.ParentId == null)
        {
            var label = EnsureLabel(items, domain, domainOrder, ref isModified);
            section.ParentId = label.Id;
            isModified = true;
            _logger.LogInformation("選單區塊 [{Key}] 已補掛到領域類別 [{Label}]。", section.Key, label.DisplayName);
        }

        return section;
    }

    /// <inheritdoc />
    public async Task SyncFunctionMenuAsync(Guid controllerMetadataId, CancellationToken cancellationToken = default)
    {
        try
        {
            var meta = await _dbContext.Set<ControllerMetadata>()
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == controllerMetadataId, cancellationToken);

            if (meta is null)
            {
                _logger.LogWarning("單一功能選單同步失敗，找不到功能中繼資料 {Id}。", controllerMetadataId);
                return;
            }

            var menuItems = await _dbContext.Set<MenuItem>().ToListAsync(cancellationToken);

            string moduleName = string.IsNullOrWhiteSpace(meta.ModuleName) ? "System" : meta.ModuleName;
            string controllerName = meta.ControllerName;
            string fallbackKey = $"PAG_{controllerName.ToUpperInvariant()}_{meta.ActionName.ToUpperInvariant()}";
            string pageKey = string.IsNullOrWhiteSpace(meta.PermissionKey) ? fallbackKey : meta.PermissionKey;

            // 1. 先找「由此功能產生」的頁面節點（以 ControllerId 識別，最可靠）
            var pageNode = menuItems.FirstOrDefault(m => m.Type == MenuType.Page && m.ControllerId == meta.Id);

            // 2. 找不到時以 Key 查找；多個功能可能共用同一權限鍵（例如同步權限元數據與系統權限清單），
            //    該權限鍵的節點若已屬於其他功能，不可沿用（否則會誤以為已存在而不建立），改用「控制器_動作」專屬 Key。
            if (pageNode == null)
            {
                var byKey = menuItems.FirstOrDefault(m => m.Key == pageKey);
                // 只有選單入口功能（IsMenu）才能認領尚未綁定功能的頁面節點；
                // 與選單共用權限鍵的非選單功能（例如詳情、下載、匯出）不得認領，否則節點會被改綁到非選單功能而被隱藏
                if (byKey != null && (byKey.ControllerId == meta.Id || (byKey.ControllerId == null && meta.IsMenu)))
                {
                    pageNode = byKey;
                }
                else if (byKey != null)
                {
                    pageKey = fallbackKey;
                    pageNode = menuItems.FirstOrDefault(m => m.Key == pageKey);
                }
            }

            bool isModified = false;

            if (meta.IsMenu)
            {
                // 1. Label(領域類別) → Section(模組)
                var sectionNode = EnsureModuleSection(
                    menuItems, moduleName, meta.ModuleTitle, meta.ModuleDomain, meta.ModuleDomainOrder,
                    meta.ModuleIcon, meta.ModuleOrder, ref isModified);

                // 2. Group
                string groupKey = $"GRP_{moduleName.ToUpperInvariant()}_{controllerName.ToUpperInvariant()}";
                var groupNode = menuItems.FirstOrDefault(m => m.Key == groupKey);
                if (groupNode == null)
                {
                    groupNode = new MenuItem
                    {
                        Id = Guid.NewGuid(),
                        ParentId = sectionNode.Id,
                        Key = groupKey,
                        DisplayName = string.IsNullOrWhiteSpace(meta.ControllerTitle) ? controllerName : meta.ControllerTitle,
                        Icon = string.IsNullOrWhiteSpace(meta.ControllerIcon) ? "fa-solid fa-layer-group" : meta.ControllerIcon,
                        DisplayOrder = meta.ControllerOrder,
                        Type = MenuType.Group,
                        ModuleName = moduleName,
                        IsActive = true,
                        IsVisible = true
                    };
                    _dbContext.Set<MenuItem>().Add(groupNode);
                    menuItems.Add(groupNode);
                    isModified = true;
                }
                else if (!groupNode.IsVisible || !groupNode.IsActive)
                {
                    // 群組曾因子節點全部隱藏而被隱藏，現在有頁面要顯示，一併恢復
                    groupNode.IsVisible = true;
                    groupNode.IsActive = true;
                    isModified = true;
                }

                // 3. Page
                if (pageNode == null)
                {
                    string defaultRoute = !string.IsNullOrWhiteSpace(meta.Path)
                        ? meta.Path
                        : $"/{moduleName.ToLowerInvariant()}/{controllerName.Replace("Controller", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant()}";

                    pageNode = new MenuItem
                    {
                        Id = Guid.NewGuid(),
                        ControllerId = meta.Id,
                        ParentId = groupNode.Id,
                        Key = pageKey,
                        DisplayName = string.IsNullOrWhiteSpace(meta.DisplayName) ? meta.ActionName : meta.DisplayName,
                        Icon = string.IsNullOrWhiteSpace(meta.Icon) ? "fa-solid fa-file" : meta.Icon,
                        Route = defaultRoute,
                        DisplayOrder = meta.DisplayOrder,
                        Type = MenuType.Page,
                        ModuleName = moduleName,
                        PermissionKey = meta.PermissionKey,
                        IsActive = true,
                        IsVisible = true
                    };
                    _dbContext.Set<MenuItem>().Add(pageNode);
                    isModified = true;
                    _logger.LogInformation("功能 [{Action}] 已加入選單，頁面節點 [{Key}]。", meta.ActionName, pageKey);
                }
                else if (!pageNode.IsVisible || !pageNode.IsActive)
                {
                    pageNode.IsVisible = true;
                    pageNode.IsActive = true;
                    isModified = true;
                    _logger.LogInformation("功能 [{Action}] 的選單頁面節點 [{Key}] 已恢復顯示。", meta.ActionName, pageKey);
                }

                // 共用同一權限鍵、但已不是選單入口的功能所遺留的頁面節點（例如改成按鈕的功能）：隱藏，避免選單重複指向同一頁面
                if (!string.IsNullOrWhiteSpace(meta.PermissionKey))
                {
                    var nonMenuIds = (await _dbContext.Set<ControllerMetadata>()
                            .AsNoTracking()
                            .Where(m => !m.IsMenu && m.PermissionKey == meta.PermissionKey)
                            .Select(m => m.Id)
                            .ToListAsync(cancellationToken))
                        .ToHashSet();

                    foreach (var stale in menuItems.Where(m =>
                                 m.Type == MenuType.Page && m.IsVisible && m.Id != pageNode.Id &&
                                 m.ControllerId.HasValue && m.ControllerId != meta.Id &&
                                 nonMenuIds.Contains(m.ControllerId.Value)).ToList())
                    {
                        stale.IsVisible = false;
                        isModified = true;
                        _logger.LogInformation("選單頁面節點 [{Key}] 的功能已不是選單入口，已隱藏。", stale.Key);
                    }
                }
            }
            else if (pageNode is { Type: MenuType.Page, IsVisible: true } && pageNode.ControllerId == meta.Id)
            {
                // 僅處理由此功能產生的頁面節點；同一權限鍵若改由其他功能提供入口則不動
                pageNode.IsVisible = false;
                isModified = true;
                _logger.LogInformation("功能 [{Action}] 已移出選單，頁面節點 [{Key}] 已隱藏。", meta.ActionName, pageKey);

                // 自動產生的群組若已無可見子節點，一併隱藏，避免出現空資料夾
                if (pageNode.ParentId.HasValue)
                {
                    var parentId = pageNode.ParentId.Value;
                    var group = menuItems.FirstOrDefault(m => m.Id == parentId);
                    if (group is { Type: MenuType.Group, IsVisible: true } &&
                        group.Key.StartsWith("GRP_", StringComparison.Ordinal) &&
                        !menuItems.Any(m => m.ParentId == parentId && m.IsVisible))
                    {
                        group.IsVisible = false;
                    }
                }
            }

            if (isModified)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行單一功能選單同步時發生未預期例外。功能識別碼: {Id}", controllerMetadataId);
            throw;
        }
    }

    public async Task SeedAndSyncMenusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. 讀取所有已經掃描完成的 ControllerMetadata 與 PermissionMetadata
            var controllerMetadatas = await _dbContext.Set<ControllerMetadata>()
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            var permissionMetadatas = await _dbContext.Set<PermissionMetadata>()
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            // 過濾出標記為選單 (IsMenu = true) 的控制器中繼資料
            var menuTargets = controllerMetadatas
                .Where(m => m.IsMenu)
                .ToList();

            if (!menuTargets.Any())
            {
                _logger.LogInformation("未偵測到任何標記為 IsMenu = true 的 API 節點，跳過選單同步。");
                return;
            }

            var existingMenuItems = await _dbContext.Set<MenuItem>().ToListAsync(cancellationToken);
            bool isModified = false;

            // 同一權限鍵僅處理一次，避免多個選單入口共用權限鍵時於每次啟動來回搬移
            var processedPageKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var vacatedGroupIds = new HashSet<Guid>();

            // 頁面對應的功能仍為選單入口時，其父級視為管理者於「選單管理」手動調整的結果，不再自動搬回預設群組
            var menuControllerIds = menuTargets.Select(m => m.Id).ToHashSet();

            // 領域類別排序：同一領域類別由多個模組宣告時，取最小值
            var domainOrders = menuTargets
                .Where(m => !string.IsNullOrWhiteSpace(m.ModuleDomain))
                .GroupBy(m => m.ModuleDomain!.Trim())
                .ToDictionary(g => g.Key, g => g.Min(m => m.ModuleDomainOrder));

            // Group By 模組 (Module) 以建立 Section（掛在領域類別 Label 之下）
            var moduleGroups = menuTargets.GroupBy(m => string.IsNullOrWhiteSpace(m.ModuleName) ? "System" : m.ModuleName);

            foreach (var moduleGroup in moduleGroups)
            {
                string moduleName = moduleGroup.Key;

                // 1.1 確保 Label(領域類別) → Section(模組)
                string? domain = moduleGroup.Select(m => m.ModuleDomain).FirstOrDefault(d => !string.IsNullOrWhiteSpace(d))?.Trim();
                int domainOrder = domain != null && domainOrders.TryGetValue(domain, out var dOrder) ? dOrder : 0;
                var sectionNode = EnsureModuleSection(
                    existingMenuItems,
                    moduleName,
                    moduleGroup.Select(m => m.ModuleTitle).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)),
                    domain,
                    domainOrder,
                    moduleGroup.Select(m => m.ModuleIcon).FirstOrDefault(i => !string.IsNullOrWhiteSpace(i)),
                    moduleGroup.Min(m => m.ModuleOrder),
                    ref isModified);

                // Group By 控制器 (Controller) 以建立第二層 Group
                var controllerGroups = moduleGroup.GroupBy(m => m.ControllerName);

                foreach (var ctrlGroup in controllerGroups)
                {
                    string controllerName = ctrlGroup.Key;
                    string groupKey = $"GRP_{moduleName.ToUpperInvariant()}_{controllerName.ToUpperInvariant()}";

                    // 1.2 確保第二層：Group (分類資料夾節點)
                    var groupNode = existingMenuItems.FirstOrDefault(m => m.Key == groupKey);
                    if (groupNode == null)
                    {
                        var sampleMeta = ctrlGroup.First();
                        groupNode = new MenuItem
                        {
                            Id = Guid.NewGuid(),
                            ParentId = sectionNode.Id,
                            Key = groupKey,
                            DisplayName = string.IsNullOrWhiteSpace(sampleMeta.ControllerTitle) ? controllerName : sampleMeta.ControllerTitle,
                            Icon = string.IsNullOrWhiteSpace(sampleMeta.ControllerIcon) ? "fa-solid fa-layer-group" : sampleMeta.ControllerIcon,
                            DisplayOrder = sampleMeta.ControllerOrder ,
                            Type = MenuType.Group,
                            ModuleName = moduleName,
                            IsActive = true,
                            IsVisible = true
                        };
                        _dbContext.Set<MenuItem>().Add(groupNode);
                        existingMenuItems.Add(groupNode);
                        isModified = true;
                    }

                    // 1.3 建立第三層：Page (頁面節點)
                    foreach (var meta in ctrlGroup)
                    {
                        string pageKey = string.IsNullOrWhiteSpace(meta.PermissionKey)
                            ? $"PAG_{controllerName.ToUpperInvariant()}_{meta.ActionName.ToUpperInvariant()}"
                            : meta.PermissionKey;

                        var pageNode = existingMenuItems.FirstOrDefault(m => m.Key == pageKey);
                        if (pageNode == null)
                        {
                            // 自動推導預設 Route (若未設定 Path)
                            string defaultRoute = !string.IsNullOrWhiteSpace(meta.Path)
                                ? meta.Path
                                : $"/{moduleName.ToLowerInvariant()}/{controllerName.Replace("Controller", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant()}";

                            pageNode = new MenuItem
                            {
                                Id = Guid.NewGuid(),
                                ControllerId = meta.Id,
                                ParentId = groupNode.Id,
                                Key = pageKey,
                                DisplayName = string.IsNullOrWhiteSpace(meta.DisplayName) ? meta.ActionName : meta.DisplayName,
                                Icon = string.IsNullOrWhiteSpace(meta.Icon) ? "fa-solid fa-file" : meta.Icon,
                                Route = defaultRoute,
                                DisplayOrder = meta.DisplayOrder,
                                Type = MenuType.Page,
                                ModuleName = moduleName,
                                PermissionKey = meta.PermissionKey,
                                IsActive = true,
                                IsVisible = true
                            };
                            _dbContext.Set<MenuItem>().Add(pageNode);
                            existingMenuItems.Add(pageNode);
                            isModified = true;
                        }
                        else if (pageNode.ParentId != groupNode.Id &&
                                 !(pageNode.ControllerId.HasValue && menuControllerIds.Contains(pageNode.ControllerId.Value)) &&
                                 processedPageKeys.Add(pageKey))
                        {
                            // 功能改由其他 Controller 提供（原 Controller 已非選單入口）：既有頁面節點改掛到新的群組
                            if (pageNode.ParentId.HasValue) vacatedGroupIds.Add(pageNode.ParentId.Value);
                            pageNode.ParentId = groupNode.Id;
                            pageNode.ControllerId = meta.Id;
                            pageNode.DisplayOrder = meta.DisplayOrder;
                            if (!string.IsNullOrWhiteSpace(meta.Path)) pageNode.Route = meta.Path;
                            isModified = true;
                            _logger.LogInformation("選單頁面 [{Key}] 已搬移至群組 [{Group}]。", pageKey, groupNode.DisplayName);
                            continue;
                        }

                        else if (!string.IsNullOrWhiteSpace(meta.Path) &&
                                 !string.Equals(pageNode.Route, meta.Path, StringComparison.OrdinalIgnoreCase))
                        {
                            // 程式碼指定的頁面路徑變更（例如新增前端頁面）：同步既有節點的路由
                            _logger.LogInformation("選單頁面 [{Key}] 路由由 {Old} 更新為 {New}。", pageKey, pageNode.Route, meta.Path);
                            pageNode.Route = meta.Path;
                            // 入口改由另一個功能提供時，名稱與圖示一併同步
                            if (!string.IsNullOrWhiteSpace(meta.DisplayName)) pageNode.DisplayName = meta.DisplayName;
                            if (!string.IsNullOrWhiteSpace(meta.Icon)) pageNode.Icon = meta.Icon;
                            pageNode.ControllerId = meta.Id;
                            isModified = true;
                        }

                        processedPageKeys.Add(pageKey);
                    }
                }
            }

            // 僅處理「中繼資料仍存在、但 IsMenu 已為 false」者；外掛模組暫時未載入（中繼資料不存在）時不隱藏
            var nonMenuControllerIds = controllerMetadatas.Where(m => !m.IsMenu).Select(m => m.Id).ToHashSet();

            // 程式碼已不再標記為選單入口的自動產生頁面（IsMenu 改為 false）：隱藏，並一併檢查其群組是否已無可見項目
            foreach (var stale in existingMenuItems.Where(m => m.Type == MenuType.Page && m.ControllerId.HasValue && m.IsVisible &&
                                                              !processedPageKeys.Contains(m.Key) &&
                                                              nonMenuControllerIds.Contains(m.ControllerId!.Value)).ToList())
            {
                stale.IsVisible = false;
                if (stale.ParentId.HasValue) vacatedGroupIds.Add(stale.ParentId.Value);
                isModified = true;
                _logger.LogInformation("選單頁面 [{Key}] 已不再是選單入口，已隱藏。", stale.Key);
            }

            // 因搬移而清空的自動產生群組：隱藏，避免選單出現空資料夾
            foreach (var groupId in vacatedGroupIds)
            {
                var group = existingMenuItems.FirstOrDefault(m => m.Id == groupId);
                if (group is { Type: MenuType.Group, IsVisible: true } &&
                    group.Key.StartsWith("GRP_", StringComparison.Ordinal) &&
                    !existingMenuItems.Any(m => m.ParentId == groupId && m.IsVisible))
                {
                    group.IsVisible = false;
                    isModified = true;
                }
            }

            if (isModified)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("動態選單樹狀結構 (Label -> Section -> Group -> Page) 種子同步完成。");
            }

            // 管理者在「系統功能管理」自訂為選單入口的功能：上面的整批同步以權限鍵為節點 Key，
            // 與其他功能共用權限鍵者會被視為「已存在」而略過，這裡逐一以單一功能同步補建。
            foreach (var customized in menuTargets.Where(m => m.IsCustomized))
            {
                await SyncFunctionMenuAsync(customized.Id, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行動態選單 SeedAndSyncMenusAsync 時發生未預期例外。");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string?> RebuildMenusAsync(CancellationToken cancellationToken = default)
    {
        string? backupPath = null;

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var existing = await _dbContext.Set<MenuItem>().ToListAsync(cancellationToken);
            if (existing.Count > 0)
            {
                // 先備份；備份失敗就中止，不刪除任何資料
                backupPath = await WriteBackupAsync(existing, cancellationToken);
                _logger.LogWarning("選單重建：已備份 {Count} 個選單節點至 {Path}，即將刪除全部節點。", existing.Count, backupPath);
            }

            // 由葉往根逐層刪除（Key 自我參照、刪除行為為 Restrict）
            var remaining = existing.ToList();
            while (remaining.Count > 0)
            {
                var parentIds = remaining.Where(m => m.ParentId.HasValue).Select(m => m.ParentId!.Value).ToHashSet();
                var leaves = remaining.Where(m => !parentIds.Contains(m.Id)).ToList();
                if (leaves.Count == 0) leaves = remaining.ToList(); // 循環參照防呆
                _dbContext.Set<MenuItem>().RemoveRange(leaves);
                await _dbContext.SaveChangesAsync(cancellationToken);
                remaining = remaining.Except(leaves).ToList();
            }

            await SeedAndSyncMenusAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        return backupPath;
    }

    private async Task<string> WriteBackupAsync(List<MenuItem> items, CancellationToken cancellationToken)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "MenuBackups");
        Directory.CreateDirectory(directory);
        string file = Path.Combine(directory, $"menuitems-{_timeProvider.GetUtcNow():yyyyMMddHHmmss}.json");

        var rows = items
            .OrderBy(m => m.ParentId.HasValue ? 1 : 0)
            .ThenBy(m => m.DisplayOrder)
            .Select(m => new
            {
                m.Id,
                m.ParentId,
                m.ControllerId,
                m.Key,
                m.DisplayName,
                m.Icon,
                m.Route,
                m.Component,
                m.DisplayOrder,
                Type = m.Type.ToString(),
                m.ModuleName,
                m.PermissionKey,
                m.IsActive,
                m.IsVisible
            });

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(rows, options), new UTF8Encoding(false), cancellationToken);
        return file;
    }
}
