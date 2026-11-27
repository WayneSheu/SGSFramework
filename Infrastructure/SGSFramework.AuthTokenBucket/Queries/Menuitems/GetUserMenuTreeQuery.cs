namespace SGSFramework.AuthTokenBucket.Queries.Menuitems;

using MediatR;
using Microsoft.AspNetCore.Http;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Permissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Identities;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Entities.Modules;
using SGSFramework.Core.Abstractions.Menus;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
    private readonly IUserRuntimeScopeService _runtimeScopeService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPermissionRegistry _permissionRegistry;
    private readonly IModuleStatusReader _moduleStatusReader;

    public GetUserMenuTreeQueryHandler(
        ITokenDbContext dbContext,
        ICurrentUserService currentUserService,
        IUserRuntimeScopeService runtimeScopeService,
        IHttpContextAccessor httpContextAccessor,
        IPermissionRegistry permissionRegistry,
        IModuleStatusReader moduleStatusReader,
        ILogger<GetUserMenuTreeQueryHandler> logger)
    {
        _permissionRegistry = permissionRegistry ?? throw new ArgumentNullException(nameof(permissionRegistry));
        _moduleStatusReader = moduleStatusReader ?? throw new ArgumentNullException(nameof(moduleStatusReader));
        _runtimeScopeService = runtimeScopeService ?? throw new ArgumentNullException(nameof(runtimeScopeService));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
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
            // JWT 不攜帶 permission claim：改以執行期權限（直接權限 + 角色 + 實驗室範圍）為準，與 API 授權檢查同源
            var userPermissions = await ResolveUserPermissionsAsync(cancellationToken).ConfigureAwait(false);
            bool isAdmin = _currentUserService.IsAdmin || userPermissions.Contains("sysadmin");

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

            // 2.1 排除對應功能已於「系統功能管理」手動停用，或功能不啟用 (IsActive = false) 的頁面
            //     (手動停用的 API 會被 FunctionStatusAuthorizationFilter 拒絕)。
            //     IsActive = false 代表程式碼已不存在該功能（模組已卸載 / 檔案已移除 / 模組停用）：
            //     模組重新註冊時 DynamicControllerRepository.RegisterAsync 會恢復仍存在的功能，故此訊號可自動復原。
            //     區段 / 群組容器於下方裁切時，若已無可見頁面會自動隱藏。
            var disabledControllerIds = await _dbContext.Set<ControllerMetadata>()
                .AsNoTracking()
                .Where(c => c.IsManuallyDisabled || !c.IsActive)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // 2.2 排除已停用模組的所有節點 (停用模組仍載入於記憶體，其 API 由 FunctionStatusAuthorizationFilter 拒絕)
            var inactiveModuleNames = await _dbContext.Set<ModuleMetadata>()
                .AsNoTracking()
                .Where(m => !m.IsActive)
                .Select(m => m.ModuleName)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // 已卸載的模組（模組管理 > 卸載）亦隱藏其選單
            inactiveModuleNames.AddRange(FunctionStatusRules.GetRemovedModuleNames());

            if (inactiveModuleNames.Count > 0)
            {
                var moduleControllerIds = await _dbContext.Set<ControllerMetadata>()
                    .AsNoTracking()
                    .Where(c => inactiveModuleNames.Contains(c.ModuleName))
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                disabledControllerIds.AddRange(moduleControllerIds);

                var inactiveSet = inactiveModuleNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
                allMenuItems.RemoveAll(m => inactiveSet.Contains(m.ModuleName));
            }

            if (disabledControllerIds.Count > 0)
            {
                var disabledSet = disabledControllerIds.ToHashSet();
                allMenuItems.RemoveAll(m => m.ControllerId.HasValue && disabledSet.Contains(m.ControllerId.Value));
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
                    // 標題 / 區段 / 群組僅為容器：必須至少有一個可見的頁面才顯示（未設權限的容器本身會被視為公開，不能單獨保留）
                    bool isContainer = node.Type is MenuType.Section or MenuType.Group or MenuType.Label;
                    if (hasAuthorizedChild || (isNodeDirectlyAuthorized && !isContainer))
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

            // 8. 各節點權限數（與「指派權限」對話框同口徑）
            await AssignPermissionCountsAsync(finalMenuTree, allMenuItems, inactiveModuleNames, cancellationToken).ConfigureAwait(false);

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

    /// <summary>
    /// 計算每個選單節點的權限數：頁面 = 其功能（Controller）的讀取 + 動作權限；區段 / 群組 = 子孫頁面的聯集（去除重複）。
    /// 已停用的權限不計入（與對話框「可選取權限」一致）。
    /// </summary>
    private async Task AssignPermissionCountsAsync(
        List<MenuItemDto> tree,
        List<MenuItem> sourceItems,
        IEnumerable<string> inactiveModuleNames,
        CancellationToken cancellationToken)
    {
        var inactive = inactiveModuleNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var disabledKeys = await _moduleStatusReader.GetDisabledPermissionKeysAsync(inactive, cancellationToken).ConfigureAwait(false);

        var registry = _permissionRegistry.GetAllPermissions()
            .Where(p => !string.IsNullOrWhiteSpace(p.PermissionKey))
            .ToList();
        var keysByController = registry
            .Where(p => !string.IsNullOrWhiteSpace(p.ControllerName))
            .GroupBy(p => p.ControllerName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(p => p.PermissionKey).ToList(), StringComparer.OrdinalIgnoreCase);
        var controllersByKey = registry
            .Where(p => !string.IsNullOrWhiteSpace(p.ControllerName))
            .GroupBy(p => p.PermissionKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(p => p.ControllerName).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), StringComparer.OrdinalIgnoreCase);

        var controllerIds = sourceItems.Where(m => m.ControllerId.HasValue).Select(m => m.ControllerId!.Value).Distinct().ToList();
        var controllerNameById = controllerIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _dbContext.Set<ControllerMetadata>().AsNoTracking()
                .Where(c => controllerIds.Contains(c.Id))
                .Select(c => new { c.Id, c.ControllerName })
                .ToListAsync(cancellationToken).ConfigureAwait(false))
                .ToDictionary(c => c.Id, c => c.ControllerName);
        var sourceById = sourceItems.ToDictionary(m => m.Id);

        HashSet<string> PageKeys(MenuItemDto page)
        {
            var controllers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (sourceById.TryGetValue(page.Id, out var src) && src.ControllerId is { } cid && controllerNameById.TryGetValue(cid, out var name))
            {
                controllers.Add(name);
            }
            var pageKey = !string.IsNullOrWhiteSpace(page.PermissionKey) ? page.PermissionKey! : page.Key;
            if (controllersByKey.TryGetValue(pageKey, out var viaKey))
            {
                controllers.UnionWith(viaKey);
            }

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var controller in controllers)
            {
                if (keysByController.TryGetValue(controller, out var list)) keys.UnionWith(list);
            }
            keys.RemoveWhere(k => disabledKeys.ContainsKey(k));
            return keys;
        }

        HashSet<string> Assign(MenuItemDto node)
        {
            var keys = node.Type == MenuType.Page
                ? PageKeys(node)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var child in node.Children) keys.UnionWith(Assign(child));
            node.PermissionCount = keys.Count;
            return keys;
        }

        foreach (var root in tree) Assign(root);
    }

    /// <summary>
    /// 取得使用者於目前實驗室（Header / Claim，未指定時為主要實驗室）的有效權限，並合併 Token 內既有的權限 Claim
    /// </summary>
    private async Task<HashSet<string>> ResolveUserPermissionsAsync(CancellationToken cancellationToken)
    {
        var permissions = new HashSet<string>(
            await _currentUserService.GetUserPermissionsAsync(cancellationToken).ConfigureAwait(false),
            StringComparer.OrdinalIgnoreCase);

        var userId = _currentUserService.UserId;
        if (string.IsNullOrWhiteSpace(userId)) return permissions;

        var httpContext = _httpContextAccessor.HttpContext;
        var labValue = httpContext?.Request.Headers["X-Lab-Id"].FirstOrDefault()
                       ?? httpContext?.Request.Headers["TenantLabId"].FirstOrDefault()
                       ?? httpContext?.Request.Headers["X-Target-Lab-Id"].FirstOrDefault()
                       ?? httpContext?.User.FindFirst("TenantLabId")?.Value
                       ?? httpContext?.User.FindFirst("lab_id")?.Value;
        Guid? labId = Guid.TryParse(labValue, out var parsed) ? parsed : null;

        var runtime = await _runtimeScopeService.GetUserPermissionsAsync(userId, labId, cancellationToken).ConfigureAwait(false);
        permissions.UnionWith(runtime);
        return permissions;
    }
}
