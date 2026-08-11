using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Menus
{
    public class MenuItemDto
    {
        /// <summary>
        /// 模組名稱 (例如: SGSFramework.System, SGSFramework.Identity, SGSFramework.AuthTokenBucket, SGSFramework.SystemLog)
        /// </summary>
        public string ModuleName { get; set; } = string.Empty;
        /// <summary>
        /// 選單識別碼 (例如: System, Identity, AuthTokenBucket, SystemLog)
        /// </summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>
        /// 選單顯示名稱
        /// </summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// 選單對應的路徑 (URL)
        /// </summary>
        public string Path { get; set; } = string.Empty;
        /// <summary>
        /// 選單圖示 (Icon)
        /// </summary>
        public string Icon { get; set; } = string.Empty;
        /// <summary>
        /// 選單排序
        /// </summary>
        public int Order { get; set; }
        /// <summary>
        /// 選單層級 (Level)，例如: 1, 2, 3
        /// </summary>
        public int Level { get; set; }
        /// <summary>
        /// 選單是否顯示 (IsDisplay)，用於控制選單的可見性
        /// </summary>
        public bool IsDisplay { get; set; }
        /// <summary>
        /// 選單所需的權限 Key (PermissionKey)，用於控制選單的存取權限
        /// </summary>
        public List<MenuItemDto> Children { get; set; } = new();
    }
}
