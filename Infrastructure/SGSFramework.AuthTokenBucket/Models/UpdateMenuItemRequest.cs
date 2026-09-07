using SGSFramework.Core.Abstractions.Entities.Controller;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Models
{
    /// <summary>
    /// 更新選單項目 HTTP 請求資料傳輸物件 (API Controller Payload)
    /// </summary>
    /// <param name="ParentId">父級選單識別碼 (Null 代表為頂層 Section 節點)</param>
    /// <param name="Key">節點識別 Key (例如: "ORG.LABORATORY.LIST")</param>
    /// <param name="Title">顯示名稱</param>
    /// <param name="Path">前端路由路徑 (僅 MenuType.Page 必須填寫)</param>
    /// <param name="Component">前端對應 Component 組件路徑 (適用於 Vue/Blazor 動態載入)</param>
    /// <param name="Icon">圖示類別 (如 FontAwesome: "fa-solid fa-gear")</param>
    /// <param name="Order">顯示排序 (同層級數字越小越靠前)</param>
    /// <param name="Type">選單類型 (Section, Group, Page)</param>
    /// <param name="ModuleName">模組識別碼 (例如: "System", "ORG")</param>
    /// <param name="PermissionKey">關聯權限鍵值</param>
    /// <param name="IsActive">是否啟用</param>
    /// <param name="IsVisible">是否顯示於導覽列</param>
    public record UpdateMenuItemRequest(
        Guid? ParentId,
        string Key,
        string Title,
        string? Path,
        string? Component,
        string Icon,
        int Order,
        MenuType Type,
        string ModuleName,
        string? PermissionKey,
        bool IsActive,
        bool IsVisible
    );
}
