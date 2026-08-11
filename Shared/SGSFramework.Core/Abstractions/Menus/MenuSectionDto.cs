using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Menus
{
    public class MenuSectionDto
    {
        /// <summary>
        /// 區塊識別碼 (例如: Host, Plugin)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 區塊顯示名稱 (例如: 主專案系統管理, 外掛擴充模組)
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 區塊圖示
        /// </summary>
        public string Icon { get; set; } = string.Empty;

        /// <summary>
        /// 區塊排序
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// 該區塊下的選單樹
        /// </summary>
        public List<MenuItemDto> Menus { get; set; } = new();
    }
}
