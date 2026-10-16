using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    /// <summary>
    /// 角色帳號狀態定義
    /// </summary>
    public enum RoleStatus
    {
        /// <summary>
        /// 已停用（暫停指派與權限生效）
        /// </summary>
        [Display(Name = "已停用")]
        Disabled = 0,

        /// <summary>
        /// 啟用中（正常運作）
        /// </summary>
        [Display(Name = "啟用中")]
        Active = 1,

        /// <summary>
        /// 已鎖定（安全異常凍結）
        /// </summary>
        [Display(Name = "已鎖定")]
        Locked = 2
    }
}
