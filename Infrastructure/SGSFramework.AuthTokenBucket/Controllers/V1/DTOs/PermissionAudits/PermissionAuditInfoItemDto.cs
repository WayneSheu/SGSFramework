using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.PermissionAudits
{
    /// <summary>
    /// 權限明細稽核 DTO (包含名稱與說明)
    /// </summary>
    public sealed class PermissionAuditInfoItemDto
    {
        /// <summary>
        /// 權限鍵值/代碼 (例如: SYSTEM.USERMANAGEMENT.READ)
        /// </summary>
        public string PermissionKey { get; set; } = string.Empty;

        /// <summary>
        /// 權限顯示標題 / 名稱
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 權限詳細描述
        /// </summary>
        public string? Description { get; set; }

        /// <summary>選單第一層（區段，例：系統管理）；依實際選單樹對應</summary>
        public string SectionTitle { get; set; } = string.Empty;

        public int SectionOrder { get; set; } = int.MaxValue;

        /// <summary>選單第二層（群組，例：角色管理）</summary>
        public string GroupTitle { get; set; } = string.Empty;

        public int GroupOrder { get; set; } = int.MaxValue;

        /// <summary>功能（Controller 標題）</summary>
        public string FunctionTitle { get; set; } = string.Empty;

        /// <summary>動作標題（例：新增、編輯）</summary>
        public string ActionTitle { get; set; } = string.Empty;

        /// <summary>此權限為選單頁面入口</summary>
        public bool IsMenuEntry { get; set; }

        public string? MenuTitle { get; set; }

        public string? MenuPath { get; set; }
    }

}
