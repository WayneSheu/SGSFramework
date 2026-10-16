using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    /// <summary>
    /// 角色範圍與分類類型
    /// </summary>
    public enum RoleScopeType
    {
        /// <summary>
        /// 系統預設角色 (不可刪除、不可變更核心權限)
        /// </summary>
        [Display(Name = "系統預設")]
        SystemDefault = 0,

        /// <summary>
        /// 全域角色 (跨實驗室 / 總部職能)
        /// </summary>
        [Display(Name = "全域角色")]
        Global = 1,

        /// <summary>
        /// 區域實驗室專屬角色 (受 TenantLabId 約束)
        /// </summary>
        [Display(Name = "實驗室專屬")]
        LabScoped = 2
    }
}
