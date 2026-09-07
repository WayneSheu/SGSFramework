using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Entities.Controller
{
    /// <summary>
    /// 選單節點類型
    /// </summary>
    public enum MenuType
    {
        /// <summary>
        /// 主專案/外掛模組區塊 (最頂層分類，如：系統管理、組織管理)
        /// </summary>
        Section = 0,

        /// <summary>
        /// 控制器/分類資料夾 (如：實驗室管理)
        /// </summary>
        Group = 1,

        /// <summary>
        /// 實體可點擊頁面 (如：實驗室清單)
        /// </summary>
        Page = 2
    }
}
