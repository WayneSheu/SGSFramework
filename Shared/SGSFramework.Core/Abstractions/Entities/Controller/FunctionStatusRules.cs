namespace SGSFramework.Core.Abstractions.Entities.Controller;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

/// <summary>
/// 系統功能 (ControllerMetadata) 手動停用的共用規則：快取鍵、比對鍵與不可停用之功能
/// </summary>
public static class FunctionStatusRules
{
    /// <summary>
    /// 手動停用功能清單 (Controller|Action) 的 IMemoryCache 快取鍵；變更功能狀態後須移除此快取
    /// </summary>
    public const string DisabledFunctionsCacheKey = "SGS_ManuallyDisabledFunctions";

    /// <summary>
    /// 系統權限清單 (權限樹) 的 IMemoryCache 快取鍵；其名稱取自 ControllerMetadata，於「系統功能管理」改名後須移除此快取
    /// </summary>
    public const string PermissionTreeCacheKey = "Cache_System_Permission_Tree";

    /// <summary>
    /// 系統功能管理控制器本身不可停用，避免管理者將自己鎖在外面而無法再恢復
    /// </summary>
    public const string ProtectedControllerName = "FunctionManagementController";

    /// <summary>
    /// 主機核心模組不可停用：其涵蓋模組管理、功能管理等所有系統管理功能，停用後將無法再恢復
    /// </summary>
    public const string ProtectedModuleName = "SGSFramework.System";

    // 「模組管理 > 卸載」的模組。卸載只移除模組資料列並自記憶體卸載；若組件直接編譯進主程式（如 ORG），
    // 控制器仍在 MVC 中，且功能 / 選單資料列保留，故需以此集合讓攔截與選單修剪視同停用，直到重新部署或重啟。
    // 僅存於記憶體：重啟時載入器會重新註冊 plugins 內的模組，狀態自然恢復，不會把模組永久鎖死。
    private static readonly ConcurrentDictionary<string, byte> RemovedModules = new(StringComparer.OrdinalIgnoreCase);

    private static long _removedModulesVersion;

    /// <summary>
    /// 已卸載模組集合的版本號：每次標記 / 解除都遞增。FunctionStatusAuthorizationFilter 的快取含已卸載模組的攔截清單，
    /// 以此偵測變動並自動重建，避免模組重新上傳後仍沿用舊快取而持續顯示「模組已卸載」。
    /// </summary>
    public static long RemovedModulesVersion => Interlocked.Read(ref _removedModulesVersion);

    /// <summary>標記模組已卸載（其 API 將被攔截、選單隱藏）</summary>
    public static void MarkModuleRemoved(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName)) return;
        RemovedModules[moduleName] = 0;
        Interlocked.Increment(ref _removedModulesVersion);
    }

    /// <summary>模組重新部署 / 註冊後解除已卸載標記</summary>
    public static void UnmarkModuleRemoved(string moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName)) return;
        if (RemovedModules.TryRemove(moduleName, out _)) Interlocked.Increment(ref _removedModulesVersion);
    }

    /// <summary>取得已卸載的模組名稱（不含核心模組）</summary>
    public static IReadOnlyCollection<string> GetRemovedModuleNames() =>
        RemovedModules.Keys.Where(n => !IsProtectedModule(n)).ToList();

    public static bool IsProtectedModule(string moduleName) =>
        string.Equals(moduleName, ProtectedModuleName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 以 Controller 類別名稱 + Action 方法名稱組成比對鍵 (與 ControllerMetadata.ControllerName / ActionName 一致)
    /// </summary>
    public static string BuildKey(string controllerName, string actionName) => $"{controllerName}|{actionName}";

    public static bool IsProtected(string controllerName) =>
        string.Equals(controllerName, ProtectedControllerName, StringComparison.OrdinalIgnoreCase);
}
