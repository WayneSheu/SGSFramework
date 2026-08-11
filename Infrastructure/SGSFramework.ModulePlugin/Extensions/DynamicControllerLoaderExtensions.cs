using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Controllers.Services;
using SGSFramework.ModulePlugin.Systems.Controller.Repositories;
using SGSFramework.ModulePlugin.Systems.Module.Loaders;
using System.ComponentModel;
using System.Reflection;

namespace SGSFramework.ModulePlugin.Extensions;

/// <summary>
/// 應用程式啟動中介軟體擴充
/// 掃描與解析 Controller 及其 Action 的中繼資料（路由、選單 [Menu]、權限 [RequiresPermission]），並同步至資料庫。
/// </summary>
public static class DynamicControllerLoaderExtensions
{
    public static async Task UseDynamicControllersAsync(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var controllerRepo = scope.ServiceProvider.GetRequiredService<IDynamicControllerRepository<ControllerMetadata>>();

        // 取得所有透過 ModuleLoader 正規載入且通過簽章驗證的合規模組名稱
        var loadedModuleNames = ModuleLoaderExtensions.GetLoadedModuleNames().ToHashSet(StringComparer.OrdinalIgnoreCase);

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            string moduleName = assembly.GetName().Name ?? "Unknown";

            // 嚴格過濾：若該 Assembly 不在合法載入清單中，直接略過，絕不寫入 ControllerMetadata
            if (!loadedModuleNames.Contains(moduleName) && !moduleName.Equals("PhysLIMS.Controller", StringComparison.OrdinalIgnoreCase) && !moduleName.Equals("SES.API", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var controllerTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && t.IsClass && t.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (controllerTypes.Count == 0) continue;

            Log.Information("[DynamicController] 開始註冊模組: {Module}, 傳入 Controller 數量: {Count}", moduleName, controllerTypes.Count);

            var newMetas = new List<ControllerMetadata>();

            foreach (var ctrlType in controllerTypes)
            {
                var routeAttrs = ctrlType.GetCustomAttributes<Microsoft.AspNetCore.Mvc.RouteAttribute>(inherit: true).ToList();
                string baseRoute = routeAttrs.FirstOrDefault()?.Template ?? $"api/{ctrlType.Name.Replace("Controller", "")}";

                var menuAttr = ctrlType.GetCustomAttribute<MenuAttribute>();
                var permAttr = ctrlType.GetCustomAttribute<RequiresPermissionAttribute>();
                var descAttr = ctrlType.GetCustomAttribute<DescriptionAttribute>();
                var orderAttr = ctrlType.GetCustomAttribute<OrderAttribute>();

                var actions = ctrlType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => m.IsPublic && !m.IsSpecialName && !m.IsDefined(typeof(Microsoft.AspNetCore.Mvc.NonActionAttribute)));

                foreach (var action in actions)
                {
                    string actionName = action.Name;
                    string routeTemplate = baseRoute;

                    var httpGet = action.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpGetAttribute>();
                    var httpPost = action.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpPostAttribute>();
                    var httpPut = action.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpPutAttribute>();
                    var httpDelete = action.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpDeleteAttribute>();

                    if (httpGet?.Template != null) routeTemplate = $"{baseRoute}/{httpGet.Template}";
                    else if (httpPost?.Template != null) routeTemplate = $"{baseRoute}/{httpPost.Template}";
                    else if (httpPut?.Template != null) routeTemplate = $"{baseRoute}/{httpPut.Template}";
                    else if (httpDelete?.Template != null) routeTemplate = $"{baseRoute}/{httpDelete.Template}";

                    var actionMenu = action.GetCustomAttribute<MenuAttribute>() ?? menuAttr;
                    var actionPerm = action.GetCustomAttribute<RequiresPermissionAttribute>() ?? permAttr;
                    var actionDesc = action.GetCustomAttribute<DescriptionAttribute>() ?? descAttr;
                    var actionOrder = action.GetCustomAttribute<OrderAttribute>() ?? orderAttr;

                    newMetas.Add(new ControllerMetadata
                    {
                        Id = Guid.NewGuid(),
                        ModuleName = moduleName,
                        ControllerName = ctrlType.Name,
                        ActionName = actionName,
                        RouteTemplate = routeTemplate,
                        DisplayName = actionMenu?.Name ?? ctrlType.Name,
                        ParentMenuName = actionMenu?.Parent,
                        Icon = actionMenu?.Icon,
                        DisplayOrder = actionOrder?.Order ?? 0,
                        PermissionKey = actionPerm?.PermissionKey ?? string.Empty,
                        Description = actionDesc?.Description,
                        ControllerTypeName = ctrlType.FullName ?? ctrlType.Name,
                        Version = assembly.GetName().Version?.ToString() ?? "1.0.0.0",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await controllerRepo.RegisterAsync(moduleName, newMetas);
            Log.Information("[DynamicController] 模組 {Module} 註冊與狀態同步處理完畢。", moduleName);
        }

        Log.Information("Controller Metadata synchronization completed successfully.");
    }
}