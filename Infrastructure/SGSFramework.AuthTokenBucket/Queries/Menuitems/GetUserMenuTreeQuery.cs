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
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Identities;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;

/// <summary>
/// 依據當前使用者權限查詢動態渲染選單樹之 Query
/// </summary>
public record GetUserMenuTreeQuery() : IRequest<Result<List<MenuItemDto>>>;

/// <summary>
/// 處理 GetUserMenuTreeQuery，負責讀取選單節點並依據使用者權限進行雙向樹狀裁切 (Tree Pruning)
/// </summary>
public class GetUserMenuTreeQueryHandler : IRequestHandler<GetUserMenuTreeQuery, Result<List<MenuItemDto>>>
{
    private readonly ITokenDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<GetUserMenuTreeQueryHandler> _logger;

    public GetUserMenuTreeQueryHandler(
        ITokenDbContext dbContext,
        ICurrentUserService currentUserService,
        ILogger<GetUserMenuTreeQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Result<List<MenuItemDto>>> Handle(GetUserMenuTreeQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // 1. 取得當前使用者權限集合與管理員身份
            var userPermissions = await _currentUserService.GetUserPermissionsAsync(cancellationToken).ConfigureAwait(false);
            bool isAdmin = _currentUserService.IsAdmin;

            // 2. 從資料庫讀取所有已啟用 (IsActive) 且可視 (IsVisible) 的選單項目
            var allMenuItems = await _dbContext.MenuItems
                .AsNoTracking()
                .Where(m => m.IsActive && m.IsVisible)
                .OrderBy(m => m.DisplayOrder)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (allMenuItems.Count == 0)
            {
                return Result.Success(new List<MenuItemDto>());
            }

            // 3. 轉為對應最新 MenuItemDto 屬性欄位之字典
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

            // 4. 權限比對：標記使用者直接具備存取權限之節點
            var authorizedItemIds = new HashSet<Guid>();

            foreach (var kvp in menuDtoDict)
            {
                Guid id = kvp.Key;
                MenuItemDto item = kvp.Value;

                // 管理員、未設定 PermissionKey 之公開選單、或持有對應 PermissionKey 者授權通過
                bool isAuthorized = isAdmin
                    || string.IsNullOrWhiteSpace(item.PermissionKey)
                    || userPermissions.Contains(item.PermissionKey);

                if (isAuthorized)
                {
                    authorizedItemIds.Add(id);
                }
            }

            // 5. 組裝父子關聯結構 (In-Memory Parent-Child Association)
            foreach (var item in menuDtoDict.Values)
            {
                if (item.ParentId.HasValue && menuDtoDict.TryGetValue(item.ParentId.Value, out var parentNode))
                {
                    parentNode.Children.Add(item);
                }
            }

            // 6. 遞迴雙向樹狀裁切 (Upward/Downward Pruning) 與同層級排序
            List<MenuItemDto> FilterAndSortTree(IEnumerable<MenuItemDto> nodes)
            {
                var resultList = new List<MenuItemDto>();

                foreach (var node in nodes)
                {
                    // 遞迴過濾子節點
                    node.Children = FilterAndSortTree(node.Children);

                    bool hasAuthorizedChild = node.Children.Count > 0;
                    bool isNodeDirectlyAuthorized = authorizedItemIds.Contains(node.Id);

                    // 保留條件：本身具備權限，或其下包含任一合法存取之子節點
                    if (isNodeDirectlyAuthorized || hasAuthorizedChild)
                    {
                        node.Children = node.Children.OrderBy(c => c.Order).ToList();
                        resultList.Add(node);
                    }
                }

                return resultList.OrderBy(n => n.Order).ToList();
            }

            // 7. 篩選頂層 Section 節點 (ParentId 為 null) 並產出最終選單樹
            var rawRoots = menuDtoDict.Values.Where(m => !m.ParentId.HasValue);
            var finalMenuTree = FilterAndSortTree(rawRoots);

            _logger.LogInformation("成功為使用者 {UserId} 計算選單樹，最終回傳 {Count} 個頂層選單容器。",
                _currentUserService.UserId, finalMenuTree.Count);

            return Result.Success(finalMenuTree);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "為使用者 {UserId} 建立動態選單樹時發生未預期例外。", _currentUserService.UserId);
            return Result.Failure<List<MenuItemDto>>(
                Error.Failure("MENU_TREE_BUILD_ERROR", "建立使用者動態選單樹時發生內部系統錯誤。"));
        }
    }
}