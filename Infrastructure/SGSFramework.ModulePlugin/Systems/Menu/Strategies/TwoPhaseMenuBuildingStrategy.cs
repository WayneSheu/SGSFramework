using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Menus;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Systems.Menu.Strategies
{
    /// <summary>
    /// 二階段授權選單構建策略 (Two-Phase Strategy)
    /// </summary>
    public class TwoPhaseMenuBuildingStrategy : IMenuBuildingStrategy
    {
        public AuthorizationMode Mode => AuthorizationMode.TwoPhase;

        private static readonly HashSet<string> CoreSystemModules = new(StringComparer.OrdinalIgnoreCase)
    {
        "SGSFramework.System",
        "SGSFramework.Identity",
        "SGSFramework.AuthTokenBucket",
        "SGSFramework.SystemLog"
    };

        public IEnumerable<MenuSectionDto> BuildMenuSections(List<ControllerMetadata> authorizedMetas)
        {
            ArgumentNullException.ThrowIfNull(authorizedMetas);

            var sections = new List<MenuSectionDto>();

            // 1. 構建 Host (主專案系統管理) 區塊
            var hostMetas = authorizedMetas
                .Where(m => string.IsNullOrEmpty(m.ModuleName) || IsCoreSystemModule(m.ModuleName))
                .ToList();

            if (hostMetas.Any())
            {
                sections.Add(new MenuSectionDto
                {
                    Name = "Host",
                    Title = "主專案系統管理",
                    Icon = "fa-solid fa-gears",
                    Order = 1,
                    Menus = BuildHierarchicalMenuTree(hostMetas, baseLevel: 1)
                });
            }

            // 2. 構建 Plugin (外掛擴充模組) 區塊
            var pluginMetas = authorizedMetas
                .Where(m => !string.IsNullOrEmpty(m.ModuleName) && !IsCoreSystemModule(m.ModuleName))
                .ToList();

            if (pluginMetas.Any())
            {
                sections.Add(new MenuSectionDto
                {
                    Name = "Plugin",
                    Title = "外掛擴充模組",
                    Icon = "fa-solid fa-puzzle-piece",
                    Order = 2,
                    Menus = BuildHierarchicalMenuTree(pluginMetas, baseLevel: 1)
                });
            }

            return sections;
        }

        private List<MenuItemDto> BuildHierarchicalMenuTree(List<ControllerMetadata> metas, int baseLevel)
        {
            var result = new List<MenuItemDto>();

            var groupedMetas = metas.GroupBy(m =>
                !string.IsNullOrWhiteSpace(m.ParentMenuName) ? m.ParentMenuName :
                !string.IsNullOrWhiteSpace(m.ModuleName) ? m.ModuleName : "通用功能");

            int groupOrder = 1;

            foreach (var group in groupedMetas)
            {
                var children = group.Select(m => new MenuItemDto
                {
                    ModuleName = m.ModuleName ?? string.Empty,
                    Name = m.ActionName ?? m.ControllerName ?? string.Empty,
                    Title = m.DisplayName ?? string.Empty,
                    Path = NormalizeRoutePath(m.RouteTemplate),
                    Icon = m.Icon ?? "fa-solid fa-link",
                    Order = m.DisplayOrder,
                    Level = baseLevel + 1,
                    IsDisplay = true,
                    ActionPermissions = !string.IsNullOrWhiteSpace(m.PermissionKey)
                        ? new List<string> { m.PermissionKey }
                        : new List<string>(),
                    Children = new List<MenuItemDto>()
                }).OrderBy(c => c.Order).ToList();

                var groupActionPermissions = group
                    .Where(m => !string.IsNullOrWhiteSpace(m.PermissionKey))
                    .Select(m => m.PermissionKey!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var parentNode = new MenuItemDto
                {
                    ModuleName = group.FirstOrDefault()?.ModuleName ?? string.Empty,
                    Name = $"Group_{group.Key.Replace(" ", "_")}",
                    Title = ResolveDisplayName(group.Key),
                    Path = string.Empty,
                    Icon = children.FirstOrDefault()?.Icon ?? "fa-solid fa-folder",
                    Order = groupOrder++,
                    Level = baseLevel,
                    IsDisplay = true,
                    ActionPermissions = groupActionPermissions,
                    Children = children
                };

                result.Add(parentNode);
            }

            return result;
        }

        private static bool IsCoreSystemModule(string moduleName)
        {
            return CoreSystemModules.Contains(moduleName) || moduleName.StartsWith("SGSFramework.System", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveDisplayName(string key)
        {
            return key.Replace("SGSFramework.Plugin.", "").Replace("SGS.Modules.", "");
        }

        private static string NormalizeRoutePath(string? routeTemplate)
        {
            if (string.IsNullOrWhiteSpace(routeTemplate)) return string.Empty;
            return routeTemplate.StartsWith('/') ? routeTemplate : $"/{routeTemplate}";
        }
    }
}
