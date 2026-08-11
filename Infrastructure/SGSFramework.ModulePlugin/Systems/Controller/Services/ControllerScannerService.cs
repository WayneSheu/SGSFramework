using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Controllers.Base;
using SGSFramework.Core.Controllers.Services;
using SGSFramework.ModulePlugin.Systems.Controller.Repositories;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace SGSFramework.ModulePlugin.Systems.Controller.Services
{
    /// <summary>
    /// 泛型控制器掃描服務，使用泛型 DbContext 以支援不同的資料庫上下文
    /// </summary>
    /// <typeparam name="TDbContext"></typeparam>
    public class ControllerScannerService<TDbContext> : IControllerScannerService<TDbContext>
        where TDbContext : DbContext
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ControllerScannerService<TDbContext>> _logger;

        public ControllerScannerService(IServiceProvider serviceProvider,ILogger<ControllerScannerService<TDbContext>> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        /// <summary>
        /// 掃描指定組件集中的控制器並註冊到 DbContext 中
        /// </summary>
        /// <param name="assemblies"></param>
        /// <returns></returns>
        public async Task ScanAndRegisterAsync(IEnumerable<Assembly> assemblies)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
            var dbSet = dbContext.Set<ControllerMetadata>();

        

            foreach (var assembly in assemblies)
            {
                //每個 Assembly 必須擁有獨立的 metadataList，避免跨組件汙染與累積
                var metadataList = new List<ControllerMetadata>();

                //只要是實作 ControllerBase 或名稱結尾為 Controller、且帶有 [ApiController] 屬性」的類別皆可納入。
                var controllers = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract &&
                                (t.IsSubclassOf(typeof(ControllerBase)) || t.Name.EndsWith("Controller")) &&
                                t.GetCustomAttribute<ApiControllerAttribute>() != null);

                foreach (var ctrl in controllers)
                {
                    var menuAttr = ctrl.GetCustomAttribute<MenuAttribute>();
                    var routeAttr = ctrl.GetCustomAttributes<RouteAttribute>().FirstOrDefault();
                    var descAttr = ctrl.GetCustomAttribute<DescriptionAttribute>(); 
                    var orderAttr = ctrl.GetCustomAttribute<OrderAttribute>();
                    var permAttr = ctrl.GetCustomAttribute<RequiresPermissionAttribute>();
                    var versionAttr = ctrl.GetCustomAttribute<ApiVersionAttribute>();

                    string version = versionAttr?.Version ?? "v1";
                    var rawRoute = routeAttr?.Template ?? $"api/{ctrl.Name.Replace("Controller", "")}";
                    var cleanRoute = rawRoute.TrimStart('/');

                    var versionPrefixes = Enumerable.Range(1, 20).Select(i => $"v{i}/").ToArray();
                    if (versionPrefixes.Any(prefix => cleanRoute.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                    {
                        cleanRoute = string.Join('/', cleanRoute.Split('/').Skip(1));
                    }

                    var controllerBaseRoute = $"{version}/{cleanRoute}";

                    // 註冊 Controller 本身 (母節點 / Index)
                    var parent = new ControllerMetadata
                    {
                        Id = Guid.NewGuid(),
                        ModuleName = assembly.GetName().Name ?? "Unknown",
                        ControllerName = ctrl.Name,
                        ControllerTypeName = ctrl.AssemblyQualifiedName ?? ctrl.FullName ?? ctrl.Name,
                        ActionName = "INDEX",
                        DisplayName = menuAttr?.Name ?? ctrl.Name,
                        Description = descAttr?.Description ?? "無描述",
                        RouteTemplate = controllerBaseRoute,
                        DisplayOrder = orderAttr?.Order ?? menuAttr?.Order ?? 10,
                        ParentMenuName = menuAttr?.Parent,
                        PermissionKey = permAttr?.PermissionKey ?? $"{ctrl.Name}_READ".ToUpper(),
                        IsActive = true,
                        Version = version,
                        CreatedAt = DateTime.UtcNow
                    };
                    metadataList.Add(parent);

                    // 註冊所有 Action (子節點)
                    var methods = ctrl.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    foreach (var method in methods)
                    {
                        var httpAttr = method.GetCustomAttributes().FirstOrDefault(a => a is HttpMethodAttribute);
                        if (httpAttr == null) continue;

                        var actionMenuAttr = method.GetCustomAttribute<MenuAttribute>();
                        var actionRouteAttr = method.GetCustomAttribute<RouteAttribute>();
                        var actionPerm = method.GetCustomAttribute<RequiresPermissionAttribute>();
                        var actionDesc = method.GetCustomAttribute<DescriptionAttribute>();
                        var actionOrder = method.GetCustomAttribute<OrderAttribute>();
                        var actionDisplayAttr = method.GetCustomAttribute<DisplayAttribute>();

                        // 名稱解析
                        string displayName = actionMenuAttr?.Name ?? actionDisplayAttr?.Name ?? method.Name;

                        // 父選單階層精確對齊邏輯
                        string parentMenuName = null;
                        if (actionMenuAttr != null && !string.IsNullOrEmpty(actionMenuAttr.Parent))
                        {
                            parentMenuName = actionMenuAttr.Parent;
                        }
                        else if (string.Equals(method.Name, "Index", StringComparison.OrdinalIgnoreCase))
                        {
                            parentMenuName = menuAttr?.Parent;
                        }
                        else
                        {
                            parentMenuName = menuAttr?.Name;
                        }

                        // 排序對齊
                        int displayOrder = actionOrder?.Order ?? actionMenuAttr?.Order ?? menuAttr?.Order ?? 10;

                        var actionRoute = actionRouteAttr?.Template ?? method.Name;

                        metadataList.Add(new ControllerMetadata
                        {
                            Id = Guid.NewGuid(),
                            ModuleName = parent.ModuleName,
                            ControllerName = ctrl.Name,
                            ControllerTypeName = ctrl.AssemblyQualifiedName ?? ctrl.FullName ?? ctrl.Name,
                            ActionName = method.Name,
                            DisplayName = displayName,
                            Description = actionDesc?.Description ?? "無描述",
                            RouteTemplate = $"{controllerBaseRoute}/{actionRoute}".Replace("//", "/"),
                            DisplayOrder = displayOrder,
                            ParentMenuName = parentMenuName,
                            PermissionKey = actionPerm?.PermissionKey ?? $"{ctrl.Name}_{method.Name}".ToUpper(),
                            IsActive = true,
                            Version = version,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }


                // 呼叫現有 Repository 的強固 Upsert 註冊邏輯
                var dynamicRepo = scope.ServiceProvider.GetRequiredService<IDynamicControllerRepository<ControllerMetadata>>();
                await dynamicRepo.RegisterAsync(assembly.GetName().Name, metadataList);
            }
        }


        /// <summary>
        /// 基於 Module + Controller 的唯一性更新或停用 Metadata。
        /// 將掃描到的 ControllerMetadata 與資料庫中的現有資料進行比對，並執行新增、更新或停用操作。
        /// </summary>
        /// <param name="dbContext"></param>
        /// <param name="dbSet"></param>
        /// <param name="newList"></param>
        /// <returns></returns>
        private async Task UpsertMetadataAsync(
                            DbContext dbContext,
                            DbSet<ControllerMetadata> dbSet,
                            List<ControllerMetadata> newList)
        {
            // 取得該模組現有資料 (效能考量：僅拉取該模組)
            var moduleName = newList.FirstOrDefault()?.ModuleName;
            var existingMetadata = await dbSet
                .Where(x => x.ModuleName == moduleName)
                .ToListAsync();

            // 建立複合鍵字典 (ControllerName + ActionName)
            var existingDict = existingMetadata.ToDictionary(k => $"{k.ControllerName}|{k.ActionName}");
            
            foreach (var newItem in newList)
            {
                var key = $"{newItem.ControllerName}|{newItem.ActionName}";

                if (existingDict.TryGetValue(key, out var existingItem))
                {
                    // --- 自動化同步點 ---
                    var type = Type.GetType(newItem.ControllerTypeName);
                    if (type != null)
                    {
                        existingItem.SyncFromAttribute(type);
                    }


                    // 更新現有資料
                    existingItem.DisplayName = newItem.DisplayName;
                    existingItem.Description = newItem.Description; // 新增欄位
                    existingItem.RouteTemplate = newItem.RouteTemplate;
                    existingItem.PermissionKey = newItem.PermissionKey;
                    existingItem.DisplayOrder = newItem.DisplayOrder;
                    existingItem.ParentMenuName = newItem.ParentMenuName;
                    existingItem.IsActive = true;
                }
                else
                {
                    // 新增資料
                    newItem.Id = Guid.NewGuid();
                    newItem.CreatedAt = DateTime.UtcNow;
                    await dbSet.AddAsync(newItem);
                }
            }

            // 處理「死連結」(軟刪除)
            var newKeys = newList.Select(c => $"{c.ControllerName}|{c.ActionName}").ToHashSet();
            foreach (var oldItem in existingMetadata.Where(x => !newKeys.Contains($"{x.ControllerName}|{x.ActionName}")))
            {
                oldItem.IsActive = false; // 不直接 Remove，改為停用
            }
        }

    }
}
