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
        public string ModuleTitle { get; init; } = string.Empty;
        public string ControllerTitle { get; init; } = string.Empty;
        public string ControllerName { get; init; } = string.Empty;
        public string ControllerTypeName { get; init; } = string.Empty;
        public string ActionName { get; init; } = string.Empty;
        public string RouteTemplate { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public string? Icon { get; init; }
        public int DisplayOrder { get; init; }
        public string? ParentMenuName { get; init; }
        public string PermissionKey { get; init; } = string.Empty;
        public bool IsActive { get; init; }

        /// <summary>是否由管理者手動停用（停用後 API 會被拒絕、選單隱藏）</summary>
        public bool IsManuallyDisabled { get; init; }

        /// <summary>停用時對使用者的公告內容</summary>
        public string? DisabledNotice { get; init; }

        public DateTimeOffset? DisabledAt { get; init; }

        /// <summary>預計恢復時間（僅供公告）</summary>
        public DateTimeOffset? ExpectedRestoreAt { get; init; }
    }
}
