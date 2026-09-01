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
using System.Text.Json;

namespace SGSFramework.ModulePlugin.Systems.Controller.Services
{
    /// <summary>
    /// 泛型控制器掃描服務，支援 Attributes 收集與 BitPosition（0~63）位元遮罩解耦
    /// </summary>
    /// <typeparam name="TDbContext">目標 EF Core DbContext 類型</typeparam>
    public class ControllerScannerService<TDbContext> : IControllerScannerService<TDbContext>
        where TDbContext : DbContext
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ControllerScannerService<TDbContext>> _logger;

        public ControllerScannerService(IServiceProvider serviceProvider, ILogger<ControllerScannerService<TDbContext>> logger)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 掃描指定組件集中的控制器並註冊到 DbContext 中
        /// </summary>
        /// <param name="assemblies">組件集合</param>
        public async Task ScanAndRegisterAsync(IEnumerable<Assembly> assemblies)
        {
            ArgumentNullException.ThrowIfNull(assemblies);

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
            var dynamicRepo = scope.ServiceProvider.GetRequiredService<IDynamicControllerRepository<ControllerMetadata>>();

            foreach (var assembly in assemblies)
            {
                try
                {
                    var metadataList = new List<ControllerMetadata>();
                    string moduleName = assembly.GetName().Name ?? "Unknown";

                    var controllers = assembly.GetTypes()
                        .Where(t => t.IsClass && !t.IsAbstract &&
                                    (t.IsSubclassOf(typeof(ControllerBase)) || t.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase)) &&
                                    t.GetCustomAttribute<ApiControllerAttribute>() != null);

                    foreach (var ctrl in controllers)
                    {
                        var menuAttr = ctrl.GetCustomAttribute<MenuAttribute>();
                        var routeAttr = ctrl.GetCustomAttributes<RouteAttribute>().FirstOrDefault();
                        var descAttr = ctrl.GetCustomAttribute<DescriptionAttribute>();
                        var orderAttr = ctrl.GetCustomAttribute<OrderAttribute>();
                        var permAttr = ctrl.GetCustomAttribute<RequiresPermissionAttribute>();
                        var versionAttr = ctrl.GetCustomAttribute<ApiVersionAttribute>();
                        var bitPosAttr = ctrl.GetCustomAttribute<BitPositionAttribute>();

                        string version = versionAttr?.Version ?? "v1";
                        var rawRoute = routeAttr?.Template ?? $"api/{ctrl.Name.Replace("Controller", "", StringComparison.OrdinalIgnoreCase)}";
                        var cleanRoute = rawRoute.TrimStart('/');

                        var versionPrefixes = Enumerable.Range(1, 20).Select(i => $"v{i}/").ToArray();
                        if (versionPrefixes.Any(prefix => cleanRoute.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                        {
                            cleanRoute = string.Join('/', cleanRoute.Split('/').Skip(1));
                        }

                        var controllerBaseRoute = $"{version}/{cleanRoute}";

                        // 收集 Controller 層級的 Attributes
                        var ctrlAttributes = ctrl.GetCustomAttributes(true)
                            .Select(attr => attr.GetType().Name)
                            .Distinct()
                            .ToList();
                        string ctrlAttributesJson = JsonSerializer.Serialize(ctrlAttributes);

                        // 註冊 Controller 本身 (母節點 / Index)
                        var parent = new ControllerMetadata
                        {
                            Id = Guid.NewGuid(),
                            ModuleName = moduleName,
                            ControllerName = ctrl.Name,
                            ControllerTypeName = ctrl.AssemblyQualifiedName ?? ctrl.FullName ?? ctrl.Name,
                            ActionName = "INDEX",
                            DisplayName = menuAttr?.Name ?? ctrl.Name,
                            Description = descAttr?.Description ?? "無描述",
                            RouteTemplate = controllerBaseRoute,
                            DisplayOrder = orderAttr?.Order ?? menuAttr?.Order ?? 10,
                            ParentMenuName = menuAttr?.Parent,
                            PermissionKey = permAttr?.PermissionKey ?? $"{ctrl.Name}_READ".ToUpper(),
                            BitPosition = bitPosAttr?.Position,
                            AttributesJson = ctrlAttributesJson,
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
                            var actionBitPosAttr = method.GetCustomAttribute<BitPositionAttribute>();

                            string displayName = actionMenuAttr?.Name ?? actionDisplayAttr?.Name ?? method.Name;

                            string? parentMenuName = null;
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

                            int displayOrder = actionOrder?.Order ?? actionMenuAttr?.Order ?? menuAttr?.Order ?? 10;
                            var actionRoute = actionRouteAttr?.Template ?? method.Name;

                            // 收集 Action 及其繼承自 Controller 的完整 Attributes
                            var actionAttributes = method.GetCustomAttributes(true)
                                .Concat(ctrl.GetCustomAttributes(true))
                                .Select(attr => attr.GetType().Name)
                                .Distinct()
                                .ToList();
                            string actionAttributesJson = JsonSerializer.Serialize(actionAttributes);

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
                                BitPosition = actionBitPosAttr?.Position ?? bitPosAttr?.Position,
                                AttributesJson = actionAttributesJson,
                                IsActive = true,
                                Version = version,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }

                    await dynamicRepo.RegisterAsync(moduleName, metadataList).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "掃描組件 {Assembly} 時發生非預期錯誤", assembly.FullName);
                    throw;
                }
            }
        }

        /// <summary>
        /// 基於 Module + Controller 的唯一性更新或停用 Metadata。
        /// </summary>
        private async Task UpsertMetadataAsync(
            DbContext dbContext,
            DbSet<ControllerMetadata> dbSet,
            List<ControllerMetadata> newList)
        {
            var moduleName = newList.FirstOrDefault()?.ModuleName;
            if (string.IsNullOrEmpty(moduleName)) return;

            var existingMetadata = await dbSet
                .Where(x => x.ModuleName == moduleName)
                .ToListAsync()
                .ConfigureAwait(false);

            var existingDict = existingMetadata.ToDictionary(k => $"{k.ControllerName}|{k.ActionName}");

            foreach (var newItem in newList)
            {
                var key = $"{newItem.ControllerName}|{newItem.ActionName}";

                if (existingDict.TryGetValue(key, out var existingItem))
                {
                    existingItem.DisplayName = newItem.DisplayName;
                    existingItem.Description = newItem.Description;
                    existingItem.RouteTemplate = newItem.RouteTemplate;
                    existingItem.PermissionKey = newItem.PermissionKey;
                    existingItem.DisplayOrder = newItem.DisplayOrder;
                    existingItem.ParentMenuName = newItem.ParentMenuName;
                    existingItem.BitPosition = newItem.BitPosition;
                    existingItem.AttributesJson = newItem.AttributesJson;
                    existingItem.IsActive = true;
                }
                else
                {
                    newItem.Id = Guid.NewGuid();
                    newItem.CreatedAt = DateTime.UtcNow;
                    await dbSet.AddAsync(newItem).ConfigureAwait(false);
                }
            }

            var newKeys = newList.Select(c => $"{c.ControllerName}|{c.ActionName}").ToHashSet();
            foreach (var oldItem in existingMetadata.Where(x => !newKeys.Contains($"{x.ControllerName}|{x.ActionName}")))
            {
                oldItem.IsActive = false;
            }
        }
    }
}