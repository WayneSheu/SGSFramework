namespace SGSFramework.Core.Abstractions.Menus;

using SGSFramework.Core.Abstractions.Entities.Controller;
using System;
using System.Collections.Generic;

/// <summary>
/// 企業級動態選單傳輸物件 (支援後台完整階層維護與前端動態渲染)
/// </summary>
public record MenuItemDto
{
    /// <summary>
    /// 唯一識別碼 (後台維護、編輯、刪除與節點移動之主要 Primary Key)
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// 父級選單識別碼 (Null 代表為頂層 Section 節點)
    /// </summary>
    public Guid? ParentId { get; init; }

    /// <summary>
    /// 節點識別 Key (例如: "ORG.LABORATORY.LIST")
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// 顯示名稱
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// 前端路由路徑 (僅 MenuType.Page 必須填寫)
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// 前端對應 Component 組件路徑 (適用於 Vue/Blazor 動態載入)
    /// </summary>
    public string? Component { get; init; }

    /// <summary>
    /// 圖示 (如 FontAwesome 類別: "fa-solid fa-gear")
    /// </summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>
    /// 顯示排序 (同層級數字越小越靠前)
    /// </summary>
    public int Order { get; init; }

    /// <summary>
    /// 選單類型 (Section: 多層級樹狀容器, Group: 選單群組, Page: 實體功能頁面)
    /// </summary>
    public MenuType Type { get; init; }

    /// <summary>
    /// 模組識別碼 (區分主專案與外掛模組，如: "System", "ORG")
    /// </summary>
    public required string ModuleName { get; init; }

    /// <summary>
    /// 關聯的權限鍵值 (用於前端 UI 元素級控制與系統授權檢核)
    /// </summary>
    public string? PermissionKey { get; init; }

    /// <summary>
    /// 是否啟用 (後台維護時需顯示停用狀態)
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// 是否顯示於導覽列 (後台維護時可檢視隱藏選單)
    /// </summary>
    public bool IsVisible { get; init; }

    /// <summary>
    /// 子選單節點集合 (僅 Type 為 Section 或 Group 時包含子節點)
    /// </summary>
    public List<MenuItemDto> Children { get; set; } = [];
}