using SGSFramework.Core.Abstractions.Entities.Controller;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Menus;

    /// <summary>
    /// 選單項目詳細資料傳輸物件 (供後台管理系統編輯與詳細資訊檢視使用)
    /// </summary>
    public record MenuItemDetailDto
    {
        /// <summary>
        /// 唯一識別碼 Primary Key
        /// </summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// 父級選單識別碼 (Null 代表為頂層 Section 節點)
        /// </summary>
        public Guid? ParentId { get; init; }

        /// <summary>
        /// 父級選單顯示名稱 (輔助後台 UI 顯示父節點名稱)
        /// </summary>
        public string? ParentTitle { get; init; }

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
        /// 圖示類別 (如 FontAwesome: "fa-solid fa-gear")
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
        /// 關聯權限鍵值
        /// </summary>
        public string? PermissionKey { get; init; }

        /// <summary>
        /// 是否啟用
        /// </summary>
        public bool IsActive { get; init; }

        /// <summary>
        /// 是否顯示於導覽列
        /// </summary>
        public bool IsVisible { get; init; }

        /// <summary>
        /// 建立時間 (UTC)
        /// </summary>
        public DateTime CreatedAtUtc { get; init; }

        /// <summary>
        /// 最後更新時間 (UTC)
        /// </summary>
        public DateTime? LastModifiedAtUtc { get; init; }
    } 

