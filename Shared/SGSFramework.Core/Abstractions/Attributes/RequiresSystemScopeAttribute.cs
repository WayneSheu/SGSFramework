#nullable enable

using System;

namespace SGSFramework.Core.Abstractions.Attributes;

/// <summary>
/// 宣告端點開放給系統帳號（M2M）呼叫，並要求權杖具備指定的操作範圍（Scope）。
/// <para>
/// 系統帳號權杖只能存取標註此屬性的端點（預設拒絕），且略過人類使用者的 <see cref="RequiresPermissionAttribute"/> 檢查；
/// 預設亦拒絕人類使用者呼叫（<see cref="AllowUsers"/> = false），對應「InternalSystemOnly」策略。
/// </para>
/// 可重複標註，需同時具備所有 Scope；未指定 Scope 時表示任何有效的系統帳號皆可呼叫。
/// </summary>
/// <example>
/// <code>
/// [HttpPost("sync")]
/// [RequiresSystemScope("inventory.write", Description = "同步庫存")]
/// public IActionResult SyncInventory() { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequiresSystemScopeAttribute : Attribute
{
    public RequiresSystemScopeAttribute()
    {
    }

    public RequiresSystemScopeAttribute(string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        Scope = scope.Trim().ToLowerInvariant();
    }

    /// <summary>必要的操作範圍（例如 inventory.write）；null 表示僅需為系統帳號</summary>
    public string? Scope { get; }

    /// <summary>Scope 說明，顯示於系統帳號管理頁的 Scope 目錄</summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否同時允許人類使用者呼叫（仍須通過其 <see cref="RequiresPermissionAttribute"/> 檢查）。預設 false。
    /// </summary>
    public bool AllowUsers { get; set; }
}
