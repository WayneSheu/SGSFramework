// ==========================================
// 檔案路徑: Infrastructure/SGSFramework.AuthTokenBucket/Services/DynamicPermissionRegistry.cs
// 架構層級: Infrastructure Layer (Service Implementation)
// ==========================================

namespace SGSFramework.AuthTokenBucket.Services;

using Microsoft.AspNetCore.Mvc;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

/// <summary>
/// 執行階段動態權限註冊表實作 (Thread-Safe，支援組件自動掃描、ModuleTitle、ControllerTitle 與 Function ActionTitle 對應)
/// </summary>
public class DynamicPermissionRegistry : IPermissionRegistry
{
    private readonly ConcurrentDictionary<string, PermissionMetadata> _permissions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, string> _reverseIndex = new();
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
    /// 掃描指定組件集合，自動擷取 ControllerTitle、Function 特性中的標題與描述，並註冊至 PermissionRegistry
    /// </summary>
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

                // 優先讀取 ControllerTitleAttribute 取得標題與描述
                var ctrlTitleAttr = ctrlType.GetCustomAttribute<ControllerTitleAttribute>();
                string ctrlTitle = ctrlTitleAttr?.Title ?? ctrlName;
                string ctrlDesc = ctrlTitleAttr?.Description ?? ctrlType.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;

                // 掃描 Controller 層級權限
                var ctrlPermAttr = ctrlType.GetCustomAttribute<RequiresPermissionAttribute>();
                if (ctrlPermAttr != null && !string.IsNullOrEmpty(ctrlPermAttr.PermissionKey))
                {
                    RegisterOrUpdatePermission(
                        permissionKey: ctrlPermAttr.PermissionKey,
                        moduleName: moduleName,
                        controllerName: ctrlName,
                        actionName: string.Empty,
                        description: string.IsNullOrEmpty(ctrlDesc) ? ctrlPermAttr.PermissionKey : ctrlDesc,
                        controllerTitle: ctrlTitle,
                        moduleTitle: moduleName,
                        actionTitle: ctrlTitle
                    );
                }

                // 掃描 Action 方法層級權限
                var methods = ctrlType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                foreach (var method in methods)
                {
                    var actionPermAttr = method.GetCustomAttribute<RequiresPermissionAttribute>();
                    if (actionPermAttr != null && !string.IsNullOrEmpty(actionPermAttr.PermissionKey))
                    {
                        // 讀取 FunctionAttribute 內的 Title 與 Description
                        var funcAttr = method.GetCustomAttribute<FunctionAttribute>();
                        string actionTitle = funcAttr?.Title ?? method.Name;
                        string actionDesc = funcAttr?.Description ?? method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;

                        if (string.IsNullOrEmpty(actionDesc))
                        {
                            actionDesc = !string.IsNullOrEmpty(ctrlDesc) ? ctrlDesc : actionPermAttr.PermissionKey;
                        }

                        RegisterOrUpdatePermission(
                            permissionKey: actionPermAttr.PermissionKey,
                            moduleName: moduleName,
                            controllerName: ctrlName,
                            actionName: method.Name,
                            description: actionDesc,
                            controllerTitle: ctrlTitle,
                            moduleTitle: moduleName,
                            actionTitle: actionTitle
                        );
                    }
                }
            }
        }
    }

    /// <summary>
    /// 取得或建立指定權限字串的 BitPosition
    /// </summary>
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
            var (moduleName, controllerName, actionName) = ParsePermissionKeyStructure(permissionKey);

            var permission = new PermissionMetadata
            {
                Id = newBitPosition + 1,
                PermissionKey = permissionKey,
                BitPosition = newBitPosition,
                ModuleName = moduleName,
                ControllerName = controllerName,
                ActionName = actionName,
                Description = permissionKey,
                ControllerTitle = controllerName,
                ModuleTitle = moduleName,
                ActionTitle = actionName
            };

            _permissions[permissionKey] = permission;
            _reverseIndex[newBitPosition] = permissionKey;
            return newBitPosition;
        }
    }

    private static (string ModuleName, string ControllerName, string ActionName) ParsePermissionKeyStructure(string permissionKey)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            return ("SGSFramework.System", "Default", "Default");
        }

        var parts = permissionKey.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            >= 3 => (parts[0], parts[1], string.Join(".", parts.Skip(2))),
            2 => ("SGSFramework.System", parts[0], parts[1]),
            1 => ("SGSFramework.System", "Default", parts[0]),
            _ => ("SGSFramework.System", "Default", permissionKey)
        };
    }

    public string? ResolvePermissionKey(string moduleName, int bitPosition)
    {
        if (_reverseIndex.TryGetValue(bitPosition, out var permissionKey))
        {
            return permissionKey;
        }

        var match = _permissions.Values.FirstOrDefault(p => p.BitPosition == bitPosition);
        return match?.PermissionKey;
    }

    public IReadOnlyCollection<PermissionMetadata> GetAllPermissions()
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

    public bool TryGetPermission(string permissionKey, out PermissionMetadata? permission)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            permission = null;
            return false;
        }

        return _permissions.TryGetValue(permissionKey, out permission);
    }

    public void Register(PermissionMetadata permission)
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
            _reverseIndex[permission.BitPosition] = permission.PermissionKey;
        }
    }

    private void RegisterOrUpdatePermission(
        string permissionKey,
        string moduleName,
        string controllerName,
        string actionName,
        string description,
        string controllerTitle = "",
        string moduleTitle = "",
        string actionTitle = "")
    {
        lock (_syncRoot)
        {
            if (_permissions.TryGetValue(permissionKey, out var existing))
            {
                if (string.IsNullOrEmpty(existing.ModuleName)) existing.ModuleName = moduleName;
                if (string.IsNullOrEmpty(existing.ControllerName)) existing.ControllerName = controllerName;
                if (string.IsNullOrEmpty(existing.ActionName)) existing.ActionName = actionName;
                if (string.IsNullOrEmpty(existing.Description) || existing.Description == permissionKey) existing.Description = description;
                if (string.IsNullOrEmpty(existing.ControllerTitle)) existing.ControllerTitle = controllerTitle;
                if (string.IsNullOrEmpty(existing.ModuleTitle)) existing.ModuleTitle = moduleTitle;
                if (string.IsNullOrEmpty(existing.ActionTitle)) existing.ActionTitle = actionTitle;

                _reverseIndex[existing.BitPosition] = permissionKey;
            }
            else
            {
                int bitPos = _currentBitIndex++;
                var permission = new PermissionMetadata
                {
                    Id = bitPos + 1,
                    PermissionKey = permissionKey,
                    BitPosition = bitPos,
                    ModuleName = moduleName,
                    ControllerName = controllerName,
                    ActionName = actionName,
                    Description = description,
                    ControllerTitle = controllerTitle,
                    ModuleTitle = string.IsNullOrEmpty(moduleTitle) ? moduleName : moduleTitle,
                    ActionTitle = string.IsNullOrEmpty(actionTitle) ? actionName : actionTitle
                };
                _permissions[permissionKey] = permission;
                _reverseIndex[bitPos] = permissionKey;
            }
        }
    }
}