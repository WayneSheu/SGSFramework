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
/// 執行階段動態權限註冊表實作 (已優化：改為以功能群組為單位的 0-63 獨立 BitPosition 編號)
/// </summary>
public class DynamicPermissionRegistry : IPermissionRegistry
{
    private readonly ConcurrentDictionary<string, PermissionMetadata> _permissions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, string> _reverseIndex = new();

    // 優化：改為追蹤每個功能群組（例如 "ORG.LABORATORY"）各自的 BitPosition 索引，確保每個群組獨立從 0 開始
    private readonly ConcurrentDictionary<string, int> _groupBitIndices = new(StringComparer.OrdinalIgnoreCase);
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

                var ctrlTitleAttr = ctrlType.GetCustomAttribute<ControllerTitleAttribute>();
                string ctrlTitle = ctrlTitleAttr?.Title ?? ctrlName;
                string ctrlDesc = ctrlTitleAttr?.Description ?? ctrlType.GetCustomAttribute<DescriptionAttribute>()?.Description ?? string.Empty;

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
                        actionTitle: ctrlTitle,
                        permissionTitle: ctrlPermAttr.PermissionTitle
                    );
                }

                var methods = ctrlType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                foreach (var method in methods)
                {
                    var actionPermAttr = method.GetCustomAttribute<RequiresPermissionAttribute>();
                    if (actionPermAttr != null && !string.IsNullOrEmpty(actionPermAttr.PermissionKey))
                    {
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
                            actionTitle: actionTitle,
                            permissionTitle: actionPermAttr.PermissionTitle
                        );
                    }
                }
            }
        }
    }

    public int GetOrCreateBitPosition(string permissionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionKey);

        var existing = _permissions.Values.FirstOrDefault(p => p.PermissionKey.Equals(permissionKey, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            return existing.BitPosition;
        }

        lock (_syncRoot)
        {
            existing = _permissions.Values.FirstOrDefault(p => p.PermissionKey.Equals(permissionKey, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing.BitPosition;
            }

            string groupKey = ExtractFeaturePrefix(permissionKey);
            int newBitPosition = _groupBitIndices.AddOrUpdate(groupKey, 0, (k, current) => current + 1);

            if (newBitPosition > 63)
            {
                throw new InvalidOperationException($"[Security Guard] 功能群組 [{groupKey}] 的權限數量已超過 64 個上限 (BitPosition: {newBitPosition})，請進行模組或功能群組拆分。");
            }

            var (moduleName, controllerName, actionName) = ParsePermissionKeyStructure(permissionKey);
            string registryKey = $"{controllerName}.{actionName}";

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
                ActionTitle = actionName,
                PermissionTitle = null
            };

            _permissions[registryKey] = permission;
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

    /// <summary>
    /// 與 PermissionBitmaskService 保持一致高效能的群組前綴擷取邏輯[cite: 18]
    /// </summary>
    private static string ExtractFeaturePrefix(string permissionKey)
    {
        var span = permissionKey.AsSpan();
        int firstDotIndex = span.IndexOf('.');

        if (firstDotIndex >= 0)
        {
            int nextDotOffset = span.Slice(firstDotIndex + 1).IndexOf('.');
            if (nextDotOffset >= 0)
            {
                return span.Slice(0, firstDotIndex + 1 + nextDotOffset).ToString();
            }
        }

        return permissionKey;
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
        var dictionary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in _permissions.Values)
        {
            if (!dictionary.ContainsKey(p.PermissionKey))
            {
                dictionary[p.PermissionKey] = p.BitPosition;
            }
        }
        return dictionary;
    }

    public bool TryGetPermission(string permissionKey, out PermissionMetadata? permission)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            permission = null;
            return false;
        }

        permission = _permissions.Values.FirstOrDefault(p => p.PermissionKey.Equals(permissionKey, StringComparison.OrdinalIgnoreCase));
        return permission != null;
    }

    public void Register(PermissionMetadata permission)
    {
        ArgumentNullException.ThrowIfNull(permission);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission.PermissionKey);

        string registryKey = $"{permission.ControllerName}.{permission.ActionName}";

        lock (_syncRoot)
        {
            if (!_permissions.ContainsKey(registryKey))
            {
                string groupKey = ExtractFeaturePrefix(permission.PermissionKey);
                permission.BitPosition = _groupBitIndices.AddOrUpdate(groupKey, 0, (k, current) => current + 1);
            }
            _permissions[registryKey] = permission;
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
        string actionTitle = "",
        string? permissionTitle = null)
    {
        string registryKey = $"{controllerName}.{actionName}";

        lock (_syncRoot)
        {
            if (_permissions.TryGetValue(registryKey, out var existing))
            {
                if (string.IsNullOrEmpty(existing.ModuleName)) existing.ModuleName = moduleName;
                if (string.IsNullOrEmpty(existing.ControllerName)) existing.ControllerName = controllerName;
                if (string.IsNullOrEmpty(existing.ActionName)) existing.ActionName = actionName;
                if (string.IsNullOrEmpty(existing.Description) || existing.Description == permissionKey) existing.Description = description;
                if (string.IsNullOrEmpty(existing.ControllerTitle)) existing.ControllerTitle = controllerTitle;
                if (string.IsNullOrEmpty(existing.ModuleTitle)) existing.ModuleTitle = moduleTitle;
                if (string.IsNullOrEmpty(existing.ActionTitle)) existing.ActionTitle = actionTitle;
                if (!string.IsNullOrEmpty(permissionTitle)) existing.PermissionTitle = permissionTitle;

                _reverseIndex[existing.BitPosition] = permissionKey;
            }
            else
            {
                string groupKey = ExtractFeaturePrefix(permissionKey);
                int bitPos = _groupBitIndices.AddOrUpdate(groupKey, 0, (k, current) => current + 1);

                if (bitPos > 63)
                {
                    throw new InvalidOperationException($"[Security Guard] 功能群組 [{groupKey}] 的權限數量已超過 64 個上限 (BitPosition: {bitPos})。");
                }

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
                    ActionTitle = string.IsNullOrEmpty(actionTitle) ? actionName : actionTitle,
                    PermissionTitle = permissionTitle
                };
                _permissions[registryKey] = permission;
                _reverseIndex[bitPos] = permissionKey;
            }
        }
    }
}