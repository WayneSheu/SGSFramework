// ==========================================
// 檔案路徑: src/SGSFramework/Infrastructure/SGSFramework.ModulePlugin/Extensions/DynamicControllerLoaderExtensions.cs
// 架構層級: Presentation / Plugin Extension Layer
// ==========================================

namespace SGSFramework.ModulePlugin.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Controllers.Services;
using SGSFramework.ModulePlugin.Systems.Controller.Repositories;
using SGSFramework.ModulePlugin.Systems.Module.Loaders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>
/// 應用程式啟動中介軟體擴充
/// 掃描與解析 Controller 及其 Action 的中繼資料（路由、選單、權限），並同步至資料庫。
/// </summary>
public static class DynamicControllerLoaderExtensions
{
    private const string DefaultVersion = "1.0.0.0";

    /// <summary>
    /// 非同步註冊並同步動態控制器中繼資料
    /// </summary>
    public static async Task UseDynamicControllersAsync(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        using var scope = app.ApplicationServices.CreateScope();
        var controllerRepo = scope.ServiceProvider.GetRequiredService<IDynamicControllerRepository<ControllerMetadata>>();

        var loadedModuleNames = ModuleLoaderExtensions.GetLoadedModuleNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            string moduleName = assembly.GetName().Name ?? "Unknown";

            if (!IsTargetModule(moduleName, loadedModuleNames))
            {
                continue;
            }

            try
            {
                await ProcessAssemblyControllersAsync(assembly, moduleName, controllerRepo).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DynamicController] 處理組件 {Module} 控制器註冊時發生未預期錯誤", moduleName);
                throw;
            }
        }

        Log.Information("控制器元資料同步已成功完成。");
    }

    private static bool IsTargetModule(string moduleName, HashSet<string> loadedModuleNames)
    {
        return loadedModuleNames.Contains(moduleName)
            || moduleName.Equals("PhysLIMS.Controller", StringComparison.OrdinalIgnoreCase)
            || moduleName.Equals("SGS.API", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task ProcessAssemblyControllersAsync(
        Assembly assembly,
        string moduleName,
        IDynamicControllerRepository<ControllerMetadata> controllerRepo)
    {
        var controllerTypes = assembly.GetTypes()
            .Where(t => !t.IsAbstract && t.IsClass && t.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (controllerTypes.Count == 0) return;

        string moduleTitle = ResolveModuleTitle(assembly, moduleName);

        Log.Information("[DynamicController] 開始註冊模組: {Module} ({ModuleTitle}), 傳入 Controller 數量: {Count}",
            moduleName, moduleTitle, controllerTypes.Count);

        var newMetas = new List<ControllerMetadata>();

        foreach (var ctrlType in controllerTypes)
        {
            ExtractControllerMetadata(assembly, moduleName, moduleTitle, ctrlType, newMetas);
        }

        await controllerRepo.RegisterAsync(moduleName, newMetas).ConfigureAwait(false);
        Log.Information("[DynamicController] 模組 {Module} 註冊與狀態同步處理完畢。", moduleName);
    }

    private static string ResolveModuleTitle(Assembly assembly, string moduleName)
    {
        var moduleAttr = assembly.GetCustomAttribute<ModuleAttribute>();
        var assemblyTitleAttr = assembly.GetCustomAttribute<AssemblyTitleAttribute>();

        if (!string.IsNullOrWhiteSpace(moduleAttr?.Title))
            return moduleAttr.Title;

        if (!string.IsNullOrWhiteSpace(assemblyTitleAttr?.Title))
            return assemblyTitleAttr.Title;

        return moduleName;
    }

    private static void ExtractControllerMetadata(
        Assembly assembly,
        string moduleName,
        string moduleTitle,
        Type ctrlType,
        List<ControllerMetadata> newMetas)
    {
        var routeAttr = ctrlType.GetCustomAttributes<RouteAttribute>(inherit: true).FirstOrDefault();
        string baseRoute = routeAttr?.Template ?? $"api/{ctrlType.Name.Replace("Controller", "", StringComparison.OrdinalIgnoreCase)}";

        // 確保路由不包含錯誤的版本前綴 (如 1.0/)
        baseRoute = CleanRouteTemplate(baseRoute);

        var ctrlTitleAttr = ctrlType.GetCustomAttribute<ControllerTitleAttribute>();
        var ctrlPermAttr = ctrlType.GetCustomAttribute<RequiresPermissionAttribute>();

        string controllerTitle = !string.IsNullOrWhiteSpace(ctrlTitleAttr?.Title)
            ? ctrlTitleAttr.Title
            : ctrlType.Name.Replace("Controller", "", StringComparison.OrdinalIgnoreCase);

        string controllerIcon = ctrlTitleAttr?.Icon ?? "fa-solid fa-folder";
        int controllerOrder = ctrlTitleAttr?.Order ?? 0;

        var actions = ctrlType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.IsPublic && !m.IsSpecialName && !m.IsDefined(typeof(NonActionAttribute)));

        foreach (var action in actions)
        {
            var metadata = CreateActionMetadata(assembly, moduleName, moduleTitle, ctrlType, ctrlTitleAttr, ctrlPermAttr, baseRoute, controllerTitle, controllerIcon, controllerOrder, action);
            newMetas.Add(metadata);
        }
    }

    private static ControllerMetadata CreateActionMetadata(
        Assembly assembly,
        string moduleName,
        string moduleTitle,
        Type ctrlType,
        ControllerTitleAttribute? ctrlTitleAttr,
        RequiresPermissionAttribute? ctrlPermAttr,
        string baseRoute,
        string controllerTitle,
        string controllerIcon,
        int controllerOrder,
        MethodInfo action)
    {
        string actionName = action.Name;
        var httpMethodAttr = action.GetCustomAttributes<HttpMethodAttribute>(inherit: true).FirstOrDefault();
        string routeTemplate = baseRoute;

        if (httpMethodAttr?.Template is { Length: > 0 } relativeOrAbsoluteRoute)
        {
            routeTemplate = relativeOrAbsoluteRoute.StartsWith('/')
                ? relativeOrAbsoluteRoute.TrimStart('/')
                : $"{baseRoute}/{relativeOrAbsoluteRoute}";
        }

        routeTemplate = CleanRouteTemplate(routeTemplate);

        var actionFuncAttr = action.GetCustomAttribute<FunctionAttribute>();
        var actionPermAttr = action.GetCustomAttribute<RequiresPermissionAttribute>() ?? ctrlPermAttr;

        string actionDisplayName = !string.IsNullOrWhiteSpace(actionFuncAttr?.Title) ? actionFuncAttr.Title : actionName;
        string actionIcon = !string.IsNullOrWhiteSpace(actionFuncAttr?.Icon) ? actionFuncAttr.Icon : controllerIcon;
        int actionOrder = actionFuncAttr?.Order ?? 0;
        string? actionDescription = actionFuncAttr?.Description ?? ctrlTitleAttr?.Description;

        bool isMenu = actionFuncAttr?.IsMenu ?? false;
        string? customPath = actionFuncAttr?.Path;

        var metadata = new ControllerMetadata
        {
            Id = Guid.NewGuid(),
            ModuleName = moduleName,
            ModuleTitle = moduleTitle,
            ControllerName = ctrlType.Name,
            ControllerTitle = controllerTitle,
            ControllerIcon = controllerIcon,
            ControllerOrder = controllerOrder,
            ActionName = actionName,
            DisplayName = actionDisplayName,
            RouteTemplate = routeTemplate,
            ParentMenuName = controllerTitle,
            Icon = actionIcon,
            DisplayOrder = actionOrder,
            IsMenu = isMenu,
            Path = customPath,
            PermissionKey = actionPermAttr?.PermissionKey ?? string.Empty,
            Description = actionDescription,
            ControllerTypeName = ctrlType.FullName ?? ctrlType.Name,
            Version = assembly.GetName().Version?.ToString() ?? DefaultVersion,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        metadata.SyncFromAttribute(ctrlType);
        return metadata;
    }

    private static string CleanRouteTemplate(string routeTemplate)
    {
        if (string.IsNullOrWhiteSpace(routeTemplate))
            return string.Empty;

        // 遞迴或使用正則移除所有開頭的硬編碼版本號（例如 "1.0/", "v1/", "v1.0/"）
        // 避免與 {version:apiVersion} 產生重複或衝突的前綴
        while (routeTemplate.StartsWith("1.0/", StringComparison.OrdinalIgnoreCase) ||
               routeTemplate.StartsWith("v1.0/", StringComparison.OrdinalIgnoreCase) ||
               routeTemplate.StartsWith("v1/", StringComparison.OrdinalIgnoreCase))
        {
            if (routeTemplate.StartsWith("1.0/", StringComparison.OrdinalIgnoreCase))
                routeTemplate = routeTemplate[4..];
            else if (routeTemplate.StartsWith("v1.0/", StringComparison.OrdinalIgnoreCase))
                routeTemplate = routeTemplate[5..];
            else if (routeTemplate.StartsWith("v1/", StringComparison.OrdinalIgnoreCase))
                routeTemplate = routeTemplate[3..];
        }

        return routeTemplate.TrimStart('/');
    }
}