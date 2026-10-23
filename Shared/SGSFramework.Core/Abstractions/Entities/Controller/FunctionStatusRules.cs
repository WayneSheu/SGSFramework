namespace SGSFramework.Core.Abstractions.Entities.Controller;

using System;

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
    /// 系統功能管理控制器本身不可停用，避免管理者將自己鎖在外面而無法再恢復
    /// </summary>
    public const string ProtectedControllerName = "FunctionManagementController";

    /// <summary>
    /// 以 Controller 類別名稱 + Action 方法名稱組成比對鍵 (與 ControllerMetadata.ControllerName / ActionName 一致)
    /// </summary>
    public static string BuildKey(string controllerName, string actionName) => $"{controllerName}|{actionName}";

    public static bool IsProtected(string controllerName) =>
        string.Equals(controllerName, ProtectedControllerName, StringComparison.OrdinalIgnoreCase);
}
