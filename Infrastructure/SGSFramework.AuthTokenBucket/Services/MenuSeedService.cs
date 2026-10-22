using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Services;

/// <summary>
/// 動態選單自動掃描與資料庫種子同步服務
/// 專注於將包含 IsMenu=true 標記之 API 自動轉換為選單階層結構 (Section -> Group -> Page)
/// </summary>
/// <typeparam name="TDbContext">資料庫上下文型別，需實作 ITokenDbContext</typeparam>
public class MenuSeedService<TDbContext> : IMenuSeedService
    where TDbContext : DbContext, ITokenDbContext
{
    private readonly TDbContext _dbContext;
    private readonly ILogger<MenuSeedService<TDbContext>> _logger;

    public MenuSeedService(
        TDbContext dbContext,
        ILogger<MenuSeedService<TDbContext>> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

            // Group By 模組 (Module) 以建立第一層 Section
            var moduleGroups = menuTargets.GroupBy(m => string.IsNullOrWhiteSpace(m.ModuleName) ? "System" : m.ModuleName);

            foreach (var moduleGroup in moduleGroups)
            {
                string moduleName = moduleGroup.Key;
                string sectionKey = $"SEC_{moduleName.ToUpperInvariant()}";

                // 1.1 確保第一層：Section (區塊節點)
                var sectionNode = existingMenuItems.FirstOrDefault(m => m.Key == sectionKey);
                if (sectionNode == null)
                {
                    string moduleTitle = moduleGroup.FirstOrDefault()?.ModuleTitle ?? moduleName;
                    sectionNode = new MenuItem
                    {
                        Id = Guid.NewGuid(),
                        Key = sectionKey,
                        DisplayName = moduleTitle,
                        Icon = "fa-solid fa-folder",
                        DisplayOrder = 10,
                        Type = MenuType.Section,
                        ModuleName = moduleName,
                        IsActive = true,
                        IsVisible = true
                    };
                    _dbContext.Set<MenuItem>().Add(sectionNode);
                    existingMenuItems.Add(sectionNode);
                    isModified = true;
                }

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
                        else if (pageNode.ParentId != groupNode.Id && processedPageKeys.Add(pageKey))
                        {
                            // 功能改由其他 Controller 提供（選單入口搬移）：既有頁面節點改掛到新的群組
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
                _logger.LogInformation("動態選單樹狀結構 (Section -> Group -> Page) 種子同步完成。");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行動態選單 SeedAndSyncMenusAsync 時發生未預期例外。");
            throw;
        }
    }
}

