using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    /// <summary>
    /// 系統控制器中繼資料傳輸物件（對應完整 ControllerMetadata 實體屬性）
    /// </summary>
    public sealed class ControllerMetadataDto
    {
        public Guid Id { get; init; }
        public string Version { get; init; } = "v1";
        public string ModuleName { get; init; } = string.Empty;
        /// <summary>模組標題（已套用「系統功能管理」的自訂名稱）</summary>
        public string ModuleTitle { get; set; } = string.Empty;

        /// <summary>Controller 標題（已套用「系統功能管理」的自訂名稱）</summary>
        public string ControllerTitle { get; set; } = string.Empty;
        public string ControllerName { get; init; } = string.Empty;
        public string ControllerTypeName { get; init; } = string.Empty;
        public string ActionName { get; init; } = string.Empty;
        public string RouteTemplate { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string? Icon { get; init; }
        public int DisplayOrder { get; init; }
        public string? ParentMenuName { get; init; }

        /// <summary>是否為選單入口（IsMenu = true 者會產生選單頁面節點）</summary>
        public bool IsMenu { get; init; }
        public string PermissionKey { get; init; } = string.Empty;
        public bool IsActive { get; init; }

        /// <summary>所屬模組是否啟用（模組於「模組管理」停用時為 false，功能視同停用）</summary>
        public bool IsModuleActive { get; set; } = true;

        /// <summary>是否由管理者手動停用（停用後 API 會被拒絕、選單隱藏）</summary>
        public bool IsManuallyDisabled { get; init; }

        /// <summary>是否由管理者自訂過顯示欄位（名稱 / 說明 / 排序 / 圖示），啟動同步不會覆蓋</summary>
        public bool IsCustomized { get; init; }

        /// <summary>停用時對使用者的公告內容</summary>
        public string? DisabledNotice { get; init; }

        public DateTimeOffset? DisabledAt { get; init; }

        /// <summary>預計恢復時間（僅供公告）</summary>
        public DateTimeOffset? ExpectedRestoreAt { get; init; }

        // === 「系統功能管理」顯示分組（已套用管理者的名稱 / 排序 / 改掛覆寫；權限歸屬仍以 ModuleName / ControllerName 為準） ===

        /// <summary>顯示於哪個模組之下</summary>
        public string GroupModuleName { get; set; } = string.Empty;
        public string GroupModuleTitle { get; set; } = string.Empty;
        public int? GroupModuleOrder { get; set; }

        /// <summary>顯示於哪個 Controller 之下（以該 Controller 的來源模組 + 名稱識別）</summary>
        public string GroupControllerModuleName { get; set; } = string.Empty;
        public string GroupControllerName { get; set; } = string.Empty;
        public string GroupControllerTitle { get; set; } = string.Empty;
        public string? GroupControllerIcon { get; set; }
        public int? GroupControllerOrder { get; set; }

        /// <summary>功能本身是否改掛到其他 Controller</summary>
        public bool IsRelocated { get; set; }

        /// <summary>所屬 Controller 是否改掛到其他模組</summary>
        public bool IsControllerRelocated { get; set; }
    }
}
