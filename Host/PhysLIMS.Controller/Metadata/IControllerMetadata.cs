using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ApiInfrastructure.Metadata
{
    /// <summary>
    /// 定義 API 三層選單結構之元資料介面。
    /// </summary>
    public interface IControllerMetadata
    {
        /// <summary>
        /// 第一層：Section / 模組大分類 (例如：身分安全與權限)
        /// </summary>
        string ModuleTitle { get; }

        /// <summary>
        /// 第二層：Controller / 功能中分類 (例如：角色權限管理)
        /// </summary>
        string ControllerTitle { get; }

        /// <summary>
        /// 第三層：Action / 單一 API 操作名稱 (例如：刪除角色)
        /// </summary>
        string DisplayName { get; }
    }
}
