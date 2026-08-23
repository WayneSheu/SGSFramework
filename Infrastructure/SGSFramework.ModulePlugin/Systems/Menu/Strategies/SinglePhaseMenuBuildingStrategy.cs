using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Menus;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Systems.Menu.Strategies
{
    /// <summary>
    /// 單階段授權選單構建策略 (Single-Phase Strategy)
    /// </summary>
    public class SinglePhaseMenuBuildingStrategy : IMenuBuildingStrategy
    {
        public AuthorizationMode Mode => AuthorizationMode.SinglePhase;

        public IEnumerable<MenuSectionDto> BuildMenuSections(List<ControllerMetadata> authorizedMetas)
        {
            ArgumentNullException.ThrowIfNull(authorizedMetas);

            // 單階段：不分 Host/Plugin 區塊，統一併入「系統選單」單一區塊，且選單結構展平為 Level 1
            var menuItems = authorizedMetas
                .Select((m, index) => new MenuItemDto
                {
                    ModuleName = m.ModuleName ?? string.Empty,
                    Name = m.ActionName ?? m.ControllerName ?? string.Empty,
                    Title = m.DisplayName ?? string.Empty,
                    Path = NormalizeRoutePath(m.RouteTemplate),
                    Icon = m.Icon ?? "fa-solid fa-link",
                    Order = m.DisplayOrder > 0 ? m.DisplayOrder : index + 1,
                    Level = 1,
                    IsDisplay = true,
                    ActionPermissions = new List<string>(), // 單階段不進行細粒度 Action 權限歸納
                    Children = new List<MenuItemDto>()
                })
                .OrderBy(m => m.Order)
                .ToList();

            return new List<MenuSectionDto>
        {
            new MenuSectionDto
            {
                Name = "Default",
                Title = "系統選單",
                Icon = "fa-solid fa-list-check",
                Order = 1,
                Menus = menuItems
            }
        };
        }

        private static string NormalizeRoutePath(string? routeTemplate)
        {
            if (string.IsNullOrWhiteSpace(routeTemplate)) return string.Empty;
            return routeTemplate.StartsWith('/') ? routeTemplate : $"/{routeTemplate}";
        }
    }
}
