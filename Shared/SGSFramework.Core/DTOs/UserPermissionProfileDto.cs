using System;
using System.Collections.Generic;

namespace SGSFramework.Core.DTOs;

/// <summary>
/// 執行期使用者跨領域/實驗室權限與選單 Profile DTO
/// </summary>
public sealed record UserPermissionProfileDto
{
    /// <summary>
    /// 使用者識別碼
    /// </summary>
    public string UserId { get; init; } = string.Empty;

    /// <summary>
    /// 當前作用中的實驗室/組織識別碼
    /// </summary>
    public Guid ActiveLabId { get; init; }

    /// <summary>
    /// 當前作用中的實驗室/組織名稱
    /// </summary>
    public string ActiveLabName { get; init; } = string.Empty;

    /// <summary>
    /// 各模組的 Bitmask 權限矩陣 (Key: 模組名稱, Value: 64 位元權限遮罩)
    /// </summary>
    public Dictionary<string, long> ModulePermissions { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 使用者具備存取權限的實驗室清單
    /// </summary>
    public List<AccessibleLabDto> AccessibleLabs { get; init; } = new();

    /// <summary>
    /// 依據權限動態產生的選單樹狀結構
    /// </summary>
    public List<MenuNodeDto> DynamicMenus { get; init; } = new();

    /// <summary>
    /// 角色清單
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();


}