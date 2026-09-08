using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Menus
{
    /// <summary>
    /// 選單建構策略類型
    /// </summary>
    public enum MenuStrategyType
    {
        /// <summary>
        /// 資料庫動態選單策略 (支援權限雙向樹狀裁切)
        /// </summary>
        DatabaseDriven = 0,

        /// <summary>
        /// 靜態/角色選單策略
        /// </summary>
        StaticRoleBased = 1
    }
}
