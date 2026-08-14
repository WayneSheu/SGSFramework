namespace SGSFramework.AuthTokenBucket.Services
{
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using SGSFramework.AuthTokenBucket.Abstractions;
    using SGSFramework.AuthTokenBucket.Models;
    using SGSFramework.Core.Abstractions.Attributes;
    using SGSFramework.Core.Abstractions.DbContexts;
    using SGSFramework.Core.Abstractions.Permissions;
    using SGSFramework.Core.Abstractions.Permissions.Identities;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// PermissionSeedService 類別負責掃描應用程式中的控制器和方法，並將其所需的權限資訊同步到資料庫中。
    /// </summary>
    /// <typeparam name="TDbContext"></typeparam>
    public class PermissionSeedService<TDbContext> : IPermissionSeedService
        where TDbContext : DbContext, ITokenDbContext
    {
        private readonly TDbContext _dbContext;
        private readonly IPermissionRegistry _permissionRegistry;
        private readonly IEnumerable<Assembly> _assembliesToScan;
        private readonly ILogger<PermissionSeedService<TDbContext>> _logger;

        public PermissionSeedService(
            TDbContext dbContext,
            IPermissionRegistry permissionRegistry,
            IEnumerable<Assembly> assembliesToScan,
            ILogger<PermissionSeedService<TDbContext>> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _permissionRegistry = permissionRegistry ?? throw new ArgumentNullException(nameof(permissionRegistry));
            _assembliesToScan = assembliesToScan ?? Enumerable.Empty<Assembly>();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SeedAndSyncPermissionsAsync(CancellationToken cancellationToken = default)
        {
            var scannedMappings = _permissionRegistry.GetAllMappings();
            if (scannedMappings == null || scannedMappings.Count == 0)
            {
                return;
            }

            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName != null &&
                           (a.FullName.StartsWith("SGSFramework") ||
                            a.FullName.StartsWith("SGS.") ||
                            a.FullName.StartsWith("PhysLIMS") ||
                            a == Assembly.GetEntryAssembly()))
                .Distinct()
                .Union(_assembliesToScan)
                .ToArray();

            var metadataMap = ExtractPermissionMetadata(allAssemblies);
            var dbPermissions = await _dbContext.Set<Permission>().ToListAsync(cancellationToken);

            bool hasChanges = false;

            foreach (var kvp in scannedMappings)
            {
                string key = kvp.Key;
                int bitPosition = kvp.Value; //直接讀取 BitPosition 索引位置

                var existing = dbPermissions.FirstOrDefault(p => string.Equals(p.PermissionKey, key, StringComparison.OrdinalIgnoreCase));

                metadataMap.TryGetValue(key, out var meta);
                string moduleName = !string.IsNullOrEmpty(meta.ModuleName) ? meta.ModuleName : "SGSFramework.System";
                string controllerName = meta.ControllerName ?? string.Empty;
                string actionName = meta.ActionName ?? string.Empty;
                string description = meta.Description ?? $"Auto-scanned permission: {key}";

                if (existing == null)
                {
                    var newPermission = new Permission
                    {
                        PermissionKey = key,
                        BitPosition = bitPosition,
                        ModuleName = moduleName,
                        ControllerName = controllerName,
                        ActionName = actionName,
                        Description = description
                    };

                    _dbContext.Set<Permission>().Add(newPermission);
                    hasChanges = true;
                    _logger.LogInformation("Auto-seeded new permission key: {Key} (BitPosition: {BitPos}, Module: {Mod}, Controller: {Ctrl}, Action: {Act})", key, bitPosition, moduleName, controllerName, actionName);
                }
                else
                {
                    bool updated = false;
                    if (existing.BitPosition != bitPosition)
                    {
                        existing.BitPosition = bitPosition;
                        updated = true;
                    }
                    if (string.IsNullOrEmpty(existing.ModuleName))
                    {
                        existing.ModuleName = moduleName;
                        updated = true;
                    }
                    if (string.IsNullOrEmpty(existing.ControllerName) && !string.IsNullOrEmpty(controllerName))
                    {
                        existing.ControllerName = controllerName;
                        updated = true;
                    }
                    if (string.IsNullOrEmpty(existing.ActionName) && !string.IsNullOrEmpty(actionName))
                    {
                        existing.ActionName = actionName;
                        updated = true;
                    }
                    if ((string.IsNullOrEmpty(existing.Description) || existing.Description.StartsWith("Auto-scanned permission:")) && !string.IsNullOrEmpty(description))
                    {
                        existing.Description = description;
                        updated = true;
                    }

                    if (updated)
                    {
                        hasChanges = true;
                        _logger.LogInformation("Updated permission metadata for key: {Key}", key);
                    }
                }
            }

            if (hasChanges)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        private static Dictionary<string, (string ModuleName, string ControllerName, string ActionName, string Description)> ExtractPermissionMetadata(IEnumerable<Assembly> assemblies)
        {
            var map = new Dictionary<string, (string, string, string, string)>(StringComparer.OrdinalIgnoreCase);

            if (assemblies == null) return map;

            foreach (var assembly in assemblies)
            {
                string assemblyName = assembly.GetName().Name ?? string.Empty;
                string moduleName = assemblyName;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray()!;
                }

                var controllerTypes = types
                    .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

                foreach (var ctrlType in controllerTypes)
                {
                    string ctrlName = ctrlType.Name;
                    string ctrlDesc = ctrlType.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;

                    var ctrlPermAttr = ctrlType.GetCustomAttribute<RequiresPermissionAttribute>();
                    if (ctrlPermAttr != null && !string.IsNullOrEmpty(ctrlPermAttr.PermissionKey))
                    {
                        if (!map.ContainsKey(ctrlPermAttr.PermissionKey))
                        {
                            map[ctrlPermAttr.PermissionKey] = (moduleName, ctrlName, string.Empty, string.IsNullOrEmpty(ctrlDesc) ? ctrlPermAttr.PermissionKey : ctrlDesc);
                        }
                    }

                    var methods = ctrlType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    foreach (var method in methods)
                    {
                        var actionPermAttr = method.GetCustomAttribute<RequiresPermissionAttribute>();
                        if (actionPermAttr != null && !string.IsNullOrEmpty(actionPermAttr.PermissionKey))
                        {
                            string actionDesc = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;
                            if (string.IsNullOrEmpty(actionDesc))
                            {
                                actionDesc = ctrlDesc;
                            }

                            map[actionPermAttr.PermissionKey] = (moduleName, ctrlName, method.Name, string.IsNullOrEmpty(actionDesc) ? actionPermAttr.PermissionKey : actionDesc);
                        }
                    }
                }
            }

            return map;
        }
    }
}