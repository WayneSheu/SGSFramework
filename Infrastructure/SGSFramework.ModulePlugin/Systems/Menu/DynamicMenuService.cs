// File: src/Infrastructure/SES.ModulePlugin/Systems/Menu/Services/DynamicMenuService.cs
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Menus;
using SGSFramework.ModulePlugin.DTOs;
using SGSFramework.ModulePlugin.Systems.Controller.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SGSFramework.ModulePlugin.Systems.Menu
{
    public class DynamicMenuService : IDynamicMenuService
    {
        private readonly IDynamicControllerRepository<ControllerMetadata> _controllerRepo;
        private readonly ILogger<DynamicMenuService> _logger;

        public DynamicMenuService(
            IDynamicControllerRepository<ControllerMetadata> controllerRepo,
            ILogger<DynamicMenuService> logger)
        {
            _controllerRepo = controllerRepo ?? throw new ArgumentNullException(nameof(controllerRepo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IEnumerable<MenuItemDto>> GetUserMenuAsync(IEnumerable<string> userPermissions)
        {
            ArgumentNullException.ThrowIfNull(userPermissions);

            try
            {
                var permissionsSet = new HashSet<string>(userPermissions, StringComparer.OrdinalIgnoreCase);

                // 取得目前系統中所有啟用中的控制器與動作項目
                var allMetas = await _controllerRepo.GetAllActiveAsync();

                // 過濾有效選單項目並依據使用者權限篩選 (若 PermissionKey 為空或使用者擁有該權限則保留)
                var menuMetas = allMetas
                    .Where(m => !string.IsNullOrEmpty(m.DisplayName)
                    && (string.IsNullOrEmpty(m.PermissionKey) || permissionsSet.Contains(m.PermissionKey))
                    )
                    .DistinctBy(m => new { m.ControllerName, m.ActionName })
                    .OrderBy(m => m.ModuleName ?? string.Empty)
                    .ThenBy(m => m.DisplayOrder)
                    .ToList();

                // 轉換為扁平選單 DTO
                var flatMenuItems = menuMetas.Select(m => new MenuItemDto
                {
                    Name = m.DisplayName!,
                    Route = $"/{m.RouteTemplate.TrimStart('/')}",
                    Icon = m.Icon ?? "bi-folder",
                    Parent = m.ParentMenuName,
                    Order = m.DisplayOrder
                }).ToList();

                // 自動補足所有作為 Parent 卻沒有對應實體項目的群組選單 (先轉為獨立 List 避免在列舉過程中修改集合)
                var existingNames = new HashSet<string>(flatMenuItems.Select(m => m.Name), StringComparer.OrdinalIgnoreCase);
                var missingParents = flatMenuItems
                    .Where(m => !string.IsNullOrEmpty(m.Parent) && !existingNames.Contains(m.Parent))
                    .Select(m => m.Parent!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(parentName => new MenuItemDto
                    {
                        Name = parentName,
                        Route = string.Empty,
                        Icon = "bi-folder",
                        Parent = null,
                        Order = 0
                    })
                    .ToList();

                flatMenuItems.AddRange(missingParents);

                // 組裝階層式選單 (Parent-Child)
                var rootMenus = flatMenuItems
                    .Where(m => string.IsNullOrEmpty(m.Parent))
                    .OrderBy(m => m.Order)
                    .ToList();

                foreach (var root in rootMenus)
                {
                    LoadChildren(root, flatMenuItems);
                }

                return rootMenus;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ">>> [DynamicMenu] 組合動態選單時發生異常。");
                return Enumerable.Empty<MenuItemDto>();
            }
        }

        private static void LoadChildren(MenuItemDto parent, List<MenuItemDto> allItems)
        {
            parent.Children = allItems
                .Where(m => string.Equals(m.Parent, parent.Name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(m => m.Order)
                .ToList();

            foreach (var child in parent.Children)
            {
                LoadChildren(child, allItems);
            }
        }
    }
}