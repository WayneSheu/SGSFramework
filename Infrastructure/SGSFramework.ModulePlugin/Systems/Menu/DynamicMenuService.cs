using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Menus;
using SGSFramework.Core.Controllers.Services;
using SGSFramework.Core.DTOs;
using SGSFramework.ModulePlugin.Systems.Controller.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SGSFramework.ModulePlugin.Systems.Services;

public class DynamicMenuService : IDynamicMenuService
{
    private readonly IDynamicControllerRepository<ControllerMetadata> _controllerRepo;
    private readonly ILogger<DynamicMenuService> _logger;

    private static readonly HashSet<string> CoreSystemModules = new(StringComparer.OrdinalIgnoreCase)
    {
        "SGSFramework.System",
        "SGSFramework.Identity",
        "SGSFramework.AuthTokenBucket",
        "SGSFramework.SystemLog"
    };

    public DynamicMenuService(
        IDynamicControllerRepository<ControllerMetadata> controllerRepo,
        ILogger<DynamicMenuService> logger)
    {
        _controllerRepo = controllerRepo ?? throw new ArgumentNullException(nameof(controllerRepo));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 實作介面要求的 GetUserMenuAsync
    /// </summary>
    public async Task<IEnumerable<MenuSectionDto>> GetUserMenuAsync(IEnumerable<string> userPermissions)
    {
        ArgumentNullException.ThrowIfNull(userPermissions);

        try
        {
            var permissionsSet = new HashSet<string>(userPermissions, StringComparer.OrdinalIgnoreCase);
            bool isSysAdmin = permissionsSet.Contains("sysadmin") || permissionsSet.Contains("*");

            var allMetas = await _controllerRepo.GetAllActiveAsync();

            var authorizedMetas = allMetas
                .Where(m => !string.IsNullOrEmpty(m.DisplayName) &&
                           (isSysAdmin || string.IsNullOrEmpty(m.PermissionKey) || permissionsSet.Contains(m.PermissionKey)))
                .DistinctBy(m => new { m.ControllerName, m.ActionName })
                .OrderBy(m => m.DisplayOrder)
                .ToList();

            var sections = new List<MenuSectionDto>();

            // 1. Host 區塊
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

            // 2. Plugin 區塊
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "根據權限生成 Section 區塊選單時發生異常。");
            return Enumerable.Empty<MenuSectionDto>();
        }
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
                Children = new List<MenuItemDto>()
            }).OrderBy(c => c.Order).ToList();

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