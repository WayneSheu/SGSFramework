namespace SGSFramework.AuthTokenBucket.Services.Strategies;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Menus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 基於資料庫與權限雙向裁切 (Tree Pruning) 的動態選單解析策略
/// </summary>
public class DatabaseMenuResolutionStrategy : IMenuResolutionStrategy
{
    private readonly ITokenDbContext _dbContext;
    private readonly ILogger<DatabaseMenuResolutionStrategy> _logger;

    public MenuStrategyType StrategyType => MenuStrategyType.DatabaseDriven;

    public DatabaseMenuResolutionStrategy(
        ITokenDbContext dbContext,
        ILogger<DatabaseMenuResolutionStrategy> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<MenuSectionDto>> BuildMenuTreeAsync(
        IEnumerable<string> permissions,
        bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        try
        {
            var userPermSet = permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var allMenuItems = await _dbContext.MenuItems
                .AsNoTracking()
                .Where(m => m.IsActive && m.IsVisible)
                .OrderBy(m => m.DisplayOrder)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (allMenuItems.Count == 0)
            {
                return [];
            }

            var menuDtoDict = allMenuItems.ToDictionary(
                m => m.Id,
                m => new MenuItemDto
                {
                    Id = m.Id,
                    ParentId = m.ParentId,
                    Key = m.Key,
                    Title = m.DisplayName,
                    Path = m.Route,
                    Component = m.Component,
                    Icon = m.Icon,
                    Order = m.DisplayOrder,
                    Type = m.Type,
                    ModuleName = m.ModuleName,
                    PermissionKey = m.PermissionKey,
                    IsActive = m.IsActive,
                    IsVisible = m.IsVisible,
                    Children = []
                });

            var authorizedItemIds = new HashSet<Guid>();
            foreach (var (id, item) in menuDtoDict)
            {
                bool isAuthorized = isAdmin
                    || string.IsNullOrWhiteSpace(item.PermissionKey)
                    || userPermSet.Contains(item.PermissionKey);

                if (isAuthorized)
                {
                    authorizedItemIds.Add(id);
                }
            }

            foreach (var item in menuDtoDict.Values)
            {
                if (item.ParentId.HasValue && menuDtoDict.TryGetValue(item.ParentId.Value, out var parentNode))
                {
                    parentNode.Children.Add(item);
                }
            }

            List<MenuItemDto> FilterAndSortTree(IEnumerable<MenuItemDto> nodes)
            {
                var resultList = new List<MenuItemDto>();
                foreach (var node in nodes)
                {
                    node.Children = FilterAndSortTree(node.Children);
                    bool hasAuthorizedChild = node.Children.Count > 0;
                    bool isNodeDirectlyAuthorized = authorizedItemIds.Contains(node.Id);

                    if (isNodeDirectlyAuthorized || hasAuthorizedChild)
                    {
                        node.Children = node.Children.OrderBy(c => c.Order).ToList();
                        resultList.Add(node);
                    }
                }
                return resultList.OrderBy(n => n.Order).ToList();
            }

            var rawRoots = menuDtoDict.Values.Where(m => !m.ParentId.HasValue);
            var prunedRoots = FilterAndSortTree(rawRoots);

            // 映射頂層 MenuItemDto 為選單區塊容器 (MenuSectionDto)
            return prunedRoots.Select(root => new MenuSectionDto
            {
                Name = root.Key,
                Title = root.Title,
                Icon = root.Icon,
                Order = root.Order,
                Menus = root.Children
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "透過資料庫策略解析選單樹時發生未預期異常。");
            return [];
        }
    }
}