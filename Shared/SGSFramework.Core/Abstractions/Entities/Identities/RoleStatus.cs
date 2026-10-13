using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    /// <summary>
    /// 角色帳號狀態定義
    /// </summary>
    public enum RoleStatus
    {
        /// <summary>
        /// 啟用中（正常運作）
        /// </summary>
        Active = 1,

        /// <summary>
        /// 已停用（暫停指派與權限生效）
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// 已鎖定（安全異常凍結）
        /// </summary>
        Locked = 2
    }
}
