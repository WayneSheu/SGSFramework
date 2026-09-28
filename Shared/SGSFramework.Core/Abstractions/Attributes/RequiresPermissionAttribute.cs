#nullable enable

using SGSFramework.Core.Abstractions.Permissions.Enums;
using System;

namespace SGSFramework.Core.Abstractions.Attributes;

/// <summary>
/// 標註於 Controller 或 Action 上，用於宣告所需的權限 Key、選擇性的權限顯示標題 (PermissionTitle)，
/// 以及操作敏感度分級 (ActionCategory)，作為動態權限樹中繼資料同步的依據。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class RequiresPermissionAttribute : Attribute
{
    /// <summary>
    /// 權限唯一識別碼 (PermissionKey)
    /// </summary>
    public string PermissionKey { get; }

    /// <summary>
    /// 權限顯示標題 (PermissionTitle)，若指定則優先作為資料庫中 PermissionTitle 的來源
    /// </summary>
    public string? PermissionTitle { get; }

    /// <summary>
    /// 操作敏感度分級 (ActionCategory)，用於判斷主區域/兼任等策略過濾邏輯
    /// Basic = 1,       // 基本操作 (View, Export)
    /// Operational = 2, // 業務操作 (Create, Update)
    /// Critical = 3,    // 關鍵操作 (Delete, Approve)
    /// Administrative = 4 // 管理操作 (ManageUsers, SystemConfig)
    /// </summary>
    public ActionCategory Category { get; }

    /// <summary>
    /// 初始化 <see cref="RequiresPermissionAttribute"/> 類別的新實例，並指定所需的權限鍵與操作分級。
    /// </summary>
    /// <param name="key">權限鍵名稱</param>
    /// <param name="category">操作敏感度分級 (預設為 Basic)</param>
    public RequiresPermissionAttribute(string key, ActionCategory category = ActionCategory.Basic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        PermissionKey = key;
        Category = category;
    }

    /// <summary>
    /// 初始化 <see cref="RequiresPermissionAttribute"/> 類別的新實例，並指定權限鍵、前端顯示標題與操作分級。
    /// </summary>
    /// <param name="key">權限鍵名稱</param>
    /// <param name="permissionTitle">權限顯示標題</param>
    /// <param name="category">操作敏感度分級 (預設為 Basic)</param>
    public RequiresPermissionAttribute(string key, string permissionTitle, ActionCategory category = ActionCategory.Basic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        PermissionKey = key;
        PermissionTitle = permissionTitle;
        Category = category;
    }
}