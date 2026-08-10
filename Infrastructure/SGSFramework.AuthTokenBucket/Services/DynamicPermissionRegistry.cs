// ==========================================
// 檔案路徑: Infrastructure/SGSFramework.AuthTokenBucket/Services/DynamicPermissionRegistry.cs
// ==========================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Abstractions.Permissions.Identities;


namespace SGSFramework.AuthTokenBucket.Services
{
    /// <summary>
    /// 執行階段動態權限註冊表實作 (Thread-Safe，支援組件自動掃描與 BitPosition 分配)
    /// </summary>
    public class DynamicPermissionRegistry : IPermissionRegistry
    {
        private readonly ConcurrentDictionary<string, Permission> _permissions = new(StringComparer.OrdinalIgnoreCase);
        private int _currentBitIndex = 0;
        private readonly object _syncRoot = new();

        public DynamicPermissionRegistry()
        {
        }

        public DynamicPermissionRegistry(IEnumerable<Assembly> assembliesToScan)
        {
            if (assembliesToScan != null)
            {
                ScanAndRegisterAssemblies(assembliesToScan);
            }
        }

        /// <summary>
        /// 掃描指定組件集合，自動擷取 RequiresPermission 特性並註冊至 PermissionRegistry (修復 CS1061)
        /// </summary>
        /// <param name="assemblies">要掃描的組件集合</param>
        public void ScanAndRegisterAssemblies(IEnumerable<Assembly> assemblies)
        {
            if (assemblies == null) return;

            foreach (var assembly in assemblies)
            {
                if (assembly == null) continue;

                string assemblyName = assembly.GetName().Name ?? string.Empty;
                string moduleName = string.IsNullOrEmpty(assemblyName) ? "SGSFramework.System" : assemblyName;

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

                    // 掃描 Controller 層級權限
                    var ctrlPermAttr = ctrlType.GetCustomAttribute<RequiresPermissionAttribute>();
                    if (ctrlPermAttr != null && !string.IsNullOrEmpty(ctrlPermAttr.PermissionKey))
                    {
                        RegisterOrUpdatePermission(
                            permissionKey: ctrlPermAttr.PermissionKey,
                            moduleName: moduleName,
                            controllerName: ctrlName,
                            actionName: string.Empty,
                            description: string.IsNullOrEmpty(ctrlDesc) ? ctrlPermAttr.PermissionKey : ctrlDesc
                        );
                    }

                    // 掃描 Action 方法層級權限
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

                            RegisterOrUpdatePermission(
                                permissionKey: actionPermAttr.PermissionKey,
                                moduleName: moduleName,
                                controllerName: ctrlName,
                                actionName: method.Name,
                                description: string.IsNullOrEmpty(actionDesc) ? actionPermAttr.PermissionKey : actionDesc
                            );
                        }
                    }
                }
            }
        }

        public int GetOrCreateBitPosition(string permissionKey)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

            if (_permissions.TryGetValue(permissionKey, out var existing))
            {
                return existing.BitPosition;
            }

            lock (_syncRoot)
            {
                if (_permissions.TryGetValue(permissionKey, out existing))
                {
                    return existing.BitPosition;
                }

                int newBitPosition = _currentBitIndex++;
                var permission = new Permission
                {
                    Id = newBitPosition + 1,
                    PermissionKey = permissionKey,
                    BitPosition = newBitPosition,
                    ModuleName = "SGSFramework.System",
                    ControllerName = "Default",
                    ActionName = "Default",
                    Description = string.Empty
                };

                _permissions[permissionKey] = permission;
                return newBitPosition;
            }
        }

        public IReadOnlyCollection<Permission> GetAllPermissions()
        {
            return _permissions.Values.ToList().AsReadOnly();
        }

        public IReadOnlyDictionary<string, int> GetAllMappings()
        {
            return _permissions.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.BitPosition,
                StringComparer.OrdinalIgnoreCase);
        }

        public bool TryGetPermission(string permissionKey, out Permission? permission)
        {
            if (string.IsNullOrWhiteSpace(permissionKey))
            {
                permission = null;
                return false;
            }

            return _permissions.TryGetValue(permissionKey, out permission);
        }

        public void Register(Permission permission)
        {
            ArgumentNullException.ThrowIfNull(permission);
            ArgumentException.ThrowIfNullOrWhiteSpace(permission.PermissionKey);

            lock (_syncRoot)
            {
                if (!_permissions.ContainsKey(permission.PermissionKey))
                {
                    permission.BitPosition = _currentBitIndex++;
                }
                _permissions[permission.PermissionKey] = permission;
            }
        }

        private void RegisterOrUpdatePermission(string permissionKey, string moduleName, string controllerName, string actionName, string description)
        {
            lock (_syncRoot)
            {
                if (_permissions.TryGetValue(permissionKey, out var existing))
                {
                    if (string.IsNullOrEmpty(existing.ModuleName)) existing.ModuleName = moduleName;
                    if (string.IsNullOrEmpty(existing.ControllerName)) existing.ControllerName = controllerName;
                    if (string.IsNullOrEmpty(existing.ActionName)) existing.ActionName = actionName;
                    if (string.IsNullOrEmpty(existing.Description)) existing.Description = description;
                }
                else
                {
                    int bitPos = _currentBitIndex++;
                    var permission = new Permission
                    {
                        Id = bitPos + 1,
                        PermissionKey = permissionKey,
                        BitPosition = bitPos,
                        ModuleName = moduleName,
                        ControllerName = controllerName,
                        ActionName = actionName,
                        Description = description
                    };
                    _permissions[permissionKey] = permission;
                }
            }
        }
    }
}