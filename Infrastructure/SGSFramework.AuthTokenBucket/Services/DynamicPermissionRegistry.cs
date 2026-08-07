namespace SGSFramework.AuthTokenBucket.Services
{
    using Microsoft.AspNetCore.Mvc;
    using SGSFramework.AuthTokenBucket.Abstractions;
    using SGSFramework.Core.Abstractions.Attributes;
    using SGSFramework.Core.Abstractions.Permissions;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// 動態權限註冊表，實現 IPermissionRegistry 介面，用於管理權限代碼及其對應的位元遮罩值。
    /// </summary>
    public class DynamicPermissionRegistry : IPermissionRegistry
    {
        private readonly ConcurrentDictionary<string, long> _registry = new(StringComparer.OrdinalIgnoreCase);
        private long _counter = 0;

        public DynamicPermissionRegistry()
        {
        }

        public DynamicPermissionRegistry(IEnumerable<Assembly> assembliesToScan)
        {
            if (assembliesToScan == null) return;

            ScanAndRegisterAssemblies(assembliesToScan);
        }

        public void ScanAndRegisterAssemblies(IEnumerable<Assembly> assemblies)
        {
            foreach (var assembly in assemblies)
            {
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
                    // 1. 掃描 Controller 層級
                    var ctrlPermAttr = ctrlType.GetCustomAttribute<RequiresPermissionAttribute>();
                    if (ctrlPermAttr != null && !string.IsNullOrEmpty(ctrlPermAttr.PermissionKey))
                    {
                        GetOrCreateMask(ctrlPermAttr.PermissionKey);
                    }

                    // 2. 掃描 Action 層級
                    var methods = ctrlType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    foreach (var method in methods)
                    {
                        var actionPermAttr = method.GetCustomAttribute<RequiresPermissionAttribute>();
                        if (actionPermAttr != null && !string.IsNullOrEmpty(actionPermAttr.PermissionKey))
                        {
                            GetOrCreateMask(actionPermAttr.PermissionKey);
                        }
                    }
                }
            }
        }

        public long GetOrCreateMask(string permissionCode)
        {
            return _registry.GetOrAdd(permissionCode, code =>
            {
                if (_counter >= 63)
                {
                    throw new InvalidOperationException($"Dynamic permission bits exceeded 64-bit limit when registering '{code}'.");
                }
                long assignedMask = 1L << (int)_counter;
                Interlocked.Increment(ref _counter);
                return assignedMask;
            });
        }

        public IReadOnlyDictionary<string, long> GetAllMappings() => _registry;
    }
}