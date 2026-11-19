namespace SGSFramework.Core.Abstractions.Entities.Controller;

using System;
using System.Collections.Generic;

/// <summary>
/// 功能的顯示位置（已套用「系統功能管理」的改掛）
/// </summary>
/// <param name="ModuleName">顯示於哪個模組之下</param>
/// <param name="ControllerModuleName">顯示於哪個 Controller 之下：該 Controller 的來源模組</param>
/// <param name="ControllerName">顯示於哪個 Controller 之下：Controller 名稱</param>
/// <param name="IsRelocated">功能本身改掛到其他 Controller</param>
/// <param name="IsControllerRelocated">所屬 Controller 改掛到其他模組</param>
public sealed record FunctionGroupPlacement(
    string ModuleName,
    string ControllerModuleName,
    string ControllerName,
    bool IsRelocated,
    bool IsControllerRelocated);

/// <summary>
/// 依 ControllerMetadata 與 <see cref="FunctionGroupOverride"/> 解析模組 / Controller 的顯示名稱、圖示、排序與改掛位置。
/// 「系統功能管理」、「系統權限清單」等畫面共用，確保同一個模組 / 功能在各處顯示的名稱一致。
/// 改掛目標已不存在（例如外掛模組未載入）時退回原分組，避免功能從畫面消失。
/// </summary>
public sealed class FunctionGroupResolver
{
    private readonly Dictionary<string, FunctionGroupOverride> _moduleOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FunctionGroupOverride> _controllerOverrides = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _moduleTitles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (string Title, string? Icon, int Order)> _controllerDefaults = new(StringComparer.OrdinalIgnoreCase);

    public FunctionGroupResolver(IEnumerable<ControllerMetadata> entities, IEnumerable<FunctionGroupOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentNullException.ThrowIfNull(overrides);

        foreach (var e in entities)
        {
            if (!_moduleTitles.TryGetValue(e.ModuleName, out var moduleTitle) || (string.IsNullOrWhiteSpace(moduleTitle) && !string.IsNullOrWhiteSpace(e.ModuleTitle)))
            {
                _moduleTitles[e.ModuleName] = e.ModuleTitle;
            }

            var key = ControllerKey(e.ModuleName, e.ControllerName);
            if (!_controllerDefaults.TryGetValue(key, out var current) || (string.IsNullOrWhiteSpace(current.Title) && !string.IsNullOrWhiteSpace(e.ControllerTitle)))
            {
                _controllerDefaults[key] = (e.ControllerTitle, e.ControllerIcon, e.ControllerOrder);
            }
        }

        foreach (var o in overrides)
        {
            if (o.Level == FunctionGroupLevel.Module) _moduleOverrides[o.ModuleName] = o;
            else _controllerOverrides[ControllerKey(o.ModuleName, o.ControllerName)] = o;
        }
    }

    public bool HasModule(string moduleName) => _moduleTitles.ContainsKey(moduleName);

    public bool HasController(string moduleName, string controllerName) => _controllerDefaults.ContainsKey(ControllerKey(moduleName, controllerName));

    /// <summary>模組顯示名稱：自訂名稱 → 程式碼標題；模組不存在於 ControllerMetadata 時回傳 null</summary>
    public string? FindModuleTitle(string moduleName)
    {
        if (_moduleOverrides.TryGetValue(moduleName, out var o) && o.Title is { } title) return title;
        if (!_moduleTitles.TryGetValue(moduleName, out var t)) return null;
        return string.IsNullOrWhiteSpace(t) ? moduleName : t;
    }

    /// <summary>Controller 顯示名稱：自訂名稱 → 程式碼標題；Controller 不存在於 ControllerMetadata 時回傳 null</summary>
    public string? FindControllerTitle(string moduleName, string controllerName)
    {
        var key = ControllerKey(moduleName, controllerName);
        if (_controllerOverrides.TryGetValue(key, out var o) && o.Title is { } title) return title;
        if (!_controllerDefaults.TryGetValue(key, out var d)) return null;
        return string.IsNullOrWhiteSpace(d.Title) ? controllerName : d.Title;
    }

    public string ModuleTitle(string moduleName) => FindModuleTitle(moduleName) ?? moduleName;

    public string ControllerTitle(string moduleName, string controllerName) => FindControllerTitle(moduleName, controllerName) ?? controllerName;

    public string? ControllerIcon(string moduleName, string controllerName)
    {
        var key = ControllerKey(moduleName, controllerName);
        if (_controllerOverrides.TryGetValue(key, out var o) && o.Icon is { } icon) return icon;
        return _controllerDefaults.TryGetValue(key, out var d) ? d.Icon : null;
    }

    /// <summary>模組自訂排序；未自訂時為 null</summary>
    public int? ModuleOrder(string moduleName) =>
        _moduleOverrides.TryGetValue(moduleName, out var o) ? o.DisplayOrder : null;

    /// <summary>Controller 自訂排序；未自訂時為 null</summary>
    public int? ControllerOrder(string moduleName, string controllerName) =>
        _controllerOverrides.TryGetValue(ControllerKey(moduleName, controllerName), out var o) ? o.DisplayOrder : null;

    /// <summary>Controller 排序：自訂排序 → 程式碼 [ControllerTitle] 的 Order</summary>
    public int EffectiveControllerOrder(string moduleName, string controllerName) =>
        ControllerOrder(moduleName, controllerName)
        ?? (_controllerDefaults.TryGetValue(ControllerKey(moduleName, controllerName), out var d) ? d.Order : int.MaxValue);

    /// <summary>解析功能的顯示位置（功能改掛 → Controller 改掛模組）</summary>
    public FunctionGroupPlacement Resolve(ControllerMetadata entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var relocated = entity is { GroupModuleName: not null, GroupControllerName: not null } &&
                        HasController(entity.GroupModuleName, entity.GroupControllerName);
        var controllerModule = relocated ? entity.GroupModuleName! : entity.ModuleName;
        var controllerName = relocated ? entity.GroupControllerName! : entity.ControllerName;

        var module = _controllerOverrides.TryGetValue(ControllerKey(controllerModule, controllerName), out var o) &&
                     o.TargetModuleName is { } target && HasModule(target)
            ? target
            : controllerModule;

        return new FunctionGroupPlacement(
            module,
            controllerModule,
            controllerName,
            relocated,
            !string.Equals(module, controllerModule, StringComparison.OrdinalIgnoreCase));
    }

    private static string ControllerKey(string moduleName, string controllerName) => $"{moduleName}|{controllerName}";
}
