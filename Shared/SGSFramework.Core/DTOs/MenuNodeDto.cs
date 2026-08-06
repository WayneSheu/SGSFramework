using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.DTOs
{

    /// <summary>
    /// 動態選單節點 DTO
    /// </summary>
    public class MenuNodeDto
    {
        /// <summary>
        /// 選單代碼 (例如: "ReportApprove", "UserList")，對應前端 View 組件名稱或權限識別點
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 選單顯示名稱 (例如: "報告簽核", "使用者管理")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 前端 Vue Router 路由路徑 (例如: "/reports/approve")
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// 選單圖示標籤或 Class (例如: "el-icon-document")
        /// </summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>
        /// 子選單節點清單 (支援無限層級)
        /// </summary>
        public List<MenuNodeDto> Children { get; set; } = new();
    }
}
