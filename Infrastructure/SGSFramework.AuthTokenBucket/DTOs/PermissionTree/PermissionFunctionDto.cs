using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs.PermissionTree
{
    public class PermissionFunctionDto
    {
        /// <summary>
        /// 功能識別名稱 (對應 ControllerName)
        /// </summary>
        public string FunctionName { get; set; } = string.Empty;

        /// <summary>
        /// 功能顯示標題 (對應 ControllerTitle)
        /// </summary>
        public string FunctionTitle { get; set; } = string.Empty;

        /// <summary>
        /// 基礎檢視權限 (READ)，作為該功能的核心前置依賴
        /// </summary>
        public PermissionDto ReadPermission { get; set; } = new();

        /// <summary>
        /// 附加異動與操作權限清單 (Create, Update, Delete 等)
        /// </summary>
        public List<PermissionDto> ActionPermissions { get; set; } = new();
    }
}
