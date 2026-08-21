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

namespace SGSFramework.ModulePlugin.Extensions;

/// <summary>
/// 應用程式啟動中介軟體擴充
/// 掃描與解析 Controller 及其 Action 的中繼資料（路由、選單、權限），並同步至資料庫。
/// 支援三層 API Menu 結構: ModuleTitle (Section) -> ControllerTitle -> DisplayName (Function)
/// </summary>
public static class DynamicControllerLoaderExtensions
{
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

            // 過濾非目標模組 Assembly
            if (!loadedModuleNames.Contains(moduleName)
                && !moduleName.Equals("PhysLIMS.Controller", StringComparison.OrdinalIgnoreCase)
                && !moduleName.Equals("SGS.API", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var controllerTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && t.IsClass && t.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (controllerTypes.Count == 0) continue;

            // 1. 解析 ModuleTitle: [ModuleAttribute] -> [AssemblyTitleAttribute] -> Assembly Name
            var moduleAttr = assembly.GetCustomAttribute<ModuleAttribute>();
            var assemblyTitleAttr = assembly.GetCustomAttribute<AssemblyTitleAttribute>();
            string moduleTitle = !string.IsNullOrWhiteSpace(moduleAttr?.Title)
                ? moduleAttr.Title
                : (!string.IsNullOrWhiteSpace(assemblyTitleAttr?.Title) ? assemblyTitleAttr.Title : moduleName);

            Log.Information("[DynamicController] 開始註冊模組: {Module} ({ModuleTitle}), 傳入 Controller 數量: {Count}",
                moduleName, moduleTitle, controllerTypes.Count);

            var newMetas = new List<ControllerMetadata>();

            foreach (var ctrlType in controllerTypes)
            {
                // 2. 解析 Route Base
                var routeAttr = ctrlType.GetCustomAttributes<RouteAttribute>(inherit: true).FirstOrDefault();
                string baseRoute = routeAttr?.Template ?? $"api/{ctrlType.Name.Replace("Controller", "", StringComparison.OrdinalIgnoreCase)}";

                // 3. 解析 ControllerTitle
                var ctrlTitleAttr = ctrlType.GetCustomAttribute<ControllerTitleAttribute>();
                var ctrlPermAttr = ctrlType.GetCustomAttribute<RequiresPermissionAttribute>();

                string controllerTitle = !string.IsNullOrWhiteSpace(ctrlTitleAttr?.Title)
                    ? ctrlTitleAttr.Title
                    : ctrlType.Name.Replace("Controller", "", StringComparison.OrdinalIgnoreCase);

                // 4. 解析 Controller 的 Action
                var actions = ctrlType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => m.IsPublic && !m.IsSpecialName && !m.IsDefined(typeof(NonActionAttribute)));

                foreach (var action in actions)
                {
                    string actionName = action.Name;

                    // 解析 HTTP Verb 與 Action 路由
                    var httpMethodAttr = action.GetCustomAttributes<HttpMethodAttribute>(inherit: true).FirstOrDefault();
                    string routeTemplate = baseRoute;

                    if (httpMethodAttr?.Template is { Length: > 0 } relativeOrAbsoluteRoute)
                    {
                        routeTemplate = relativeOrAbsoluteRoute.StartsWith('/')
                            ? relativeOrAbsoluteRoute.TrimStart('/')
                            : $"{baseRoute}/{relativeOrAbsoluteRoute}";
                    }

                    // 5. 解析 FunctionAttribute (Action 級別)
                    var actionFuncAttr = action.GetCustomAttribute<FunctionAttribute>();
                    var actionPermAttr = action.GetCustomAttribute<RequiresPermissionAttribute>() ?? ctrlPermAttr;

                    // 各屬性賦值與 Fallback 機制
                    string actionDisplayName = !string.IsNullOrWhiteSpace(actionFuncAttr?.Title) ? actionFuncAttr.Title : actionName;
                    string actionIcon = actionFuncAttr?.Icon ?? ctrlTitleAttr?.Icon ?? string.Empty;
                    int actionOrder = actionFuncAttr?.Order ?? ctrlTitleAttr?.Order ?? 0;
                    string? actionDescription = actionFuncAttr?.Description ?? ctrlTitleAttr?.Description;

                    newMetas.Add(new ControllerMetadata
                    {
                        Id = Guid.NewGuid(),
                        ModuleName = moduleName,
                        ModuleTitle = moduleTitle,                  // 寫入 core.ControllerMetadata.ModuleTitle
                        ControllerName = ctrlType.Name,
                        ControllerTitle = controllerTitle,          // 寫入 core.ControllerMetadata.ControllerTitle
                        ActionName = actionName,
                        DisplayName = actionDisplayName,            // 寫入 DisplayName
                        RouteTemplate = routeTemplate,
                        ParentMenuName = controllerTitle,
                        Icon = actionIcon,                          // 寫入 Icon
                        DisplayOrder = actionOrder,                  // 寫入 DisplayOrder
                        PermissionKey = actionPermAttr?.PermissionKey ?? string.Empty,
                        Description = actionDescription,            // 寫入 Description
                        ControllerTypeName = ctrlType.FullName ?? ctrlType.Name,
                        Version = assembly.GetName().Version?.ToString() ?? "1.0.0.0",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            // 將解析好的元資料同步寫入 Repository / DB
            await controllerRepo.RegisterAsync(moduleName, newMetas);
            Log.Information("[DynamicController] 模組 {Module} 註冊與狀態同步處理完畢。", moduleName);
        }

        Log.Information("控制器元資料同步已成功完成。");
    }
}