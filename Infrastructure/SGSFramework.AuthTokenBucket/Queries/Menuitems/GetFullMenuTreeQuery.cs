namespace SGSFramework.AuthTokenBucket.Queries.Menuitems;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.DTOs.MenuItems;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;

/// <summary>
/// 取得完整選單管理樹 (包含已停用與隱藏節點，供後台選單管理維護使用) 之 Query
/// </summary>
public record GetFullMenuTreeQuery() : IRequest<Result<List<MenuItemDto>>>;

/// <summary>
/// 處理 GetFullMenuTreeQuery，負責讀取資料庫中所有選單節點並建構完整階層結構樹 (不受使用者權限或啟用狀態裁切)
/// </summary>
public class GetFullMenuTreeQueryHandler : IRequestHandler<GetFullMenuTreeQuery, Result<List<MenuItemDto>>>
{
    private readonly ITokenDbContext _dbContext;
    private readonly ILogger<GetFullMenuTreeQueryHandler> _logger;

    public GetFullMenuTreeQueryHandler(
        ITokenDbContext dbContext,
        ILogger<GetFullMenuTreeQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<List<MenuItemDto>>> Handle(GetFullMenuTreeQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // 1. 從資料庫讀取「所有」選單項目 (使用強型別 MenuItems 屬性，避免 CS1061)
            var allMenuItems = await _dbContext.MenuItems
                .AsNoTracking()
                .OrderBy(m => m.DisplayOrder)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (allMenuItems.Count == 0)
            {
                return Result.Success(new List<MenuItemDto>());
            }

            // 2. 對應為最新 MenuItemDto 屬性欄位之字典 (O(1) 節點索引)
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

            // 3. 進行記憶體中樹狀階層組裝 (In-Memory Parent-Child Association)
            var rootNodes = new List<MenuItemDto>();

            foreach (var item in menuDtoDict.Values)
            {
                if (item.ParentId.HasValue && menuDtoDict.TryGetValue(item.ParentId.Value, out var parentNode))
                {
                    parentNode.Children.Add(item);
                }
                else
                {
                    // 無 ParentId 或找不到父級者作為頂層 Section 節點
                    rootNodes.Add(item);
                }
            }

            // 4. 遞迴進行各階層排序 (依 Order 升遞排序)
            List<MenuItemDto> SortTreeRecursively(IEnumerable<MenuItemDto> nodes)
            {
                var sortedList = nodes.OrderBy(n => n.Order).ToList();
                foreach (var node in sortedList)
                {
                    if (node.Children.Count > 0)
                    {
                        node.Children = SortTreeRecursively(node.Children);
                    }
                }
                return sortedList;
            }

            var finalFullTree = SortTreeRecursively(rootNodes);

            _logger.LogInformation("成功產生完整選單管理樹，總計包含 {TotalCount} 個節點，頂層容器共 {RootCount} 個。",
                allMenuItems.Count, finalFullTree.Count);

            return Result.Success(finalFullTree);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "執行 GetFullMenuTreeQuery 時發生未預期例外。");
            return Result.Failure<List<MenuItemDto>>(
                Error.Failure("MENU_FULL_TREE_ERROR", "讀取完整選單管理樹時發生內部系統錯誤。"));
        }
    }
}