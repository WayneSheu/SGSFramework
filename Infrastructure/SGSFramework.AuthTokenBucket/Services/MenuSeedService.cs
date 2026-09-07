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
                    }
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

