using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Menus
{
    /// <summary>
    /// 授權模式列舉
    /// </summary>
    public enum AuthorizationMode
    {
        /// <summary>
        /// 單階段授權：權限不分層，僅回傳展平的 Level 1 選單，不進行區塊與按鈕級別權限歸納
        /// </summary>
        SinglePhase = 1,

        /// <summary>
        /// 二階段授權：支援 Section 區塊、Level 1/2/3 階層選單與 ActionPermissions 細粒度控制點歸納
        /// </summary>
        TwoPhase = 2
    }
}
