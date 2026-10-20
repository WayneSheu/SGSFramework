using SGSFramework.Core.Abstractions.Entities.Identities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.Identity.DTOs.Roles
{
    public sealed class CreateRoleRequest
    {
        /// <summary>
        /// 角色代碼
        /// </summary>
        /// <example>LAB_MANAGER</example>
        [Display(Name = "角色代碼")]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// 角色名稱 (必須唯一)
        /// </summary>
        /// <example>實驗室資深主管</example>
        [Display(Name = "角色名稱")]
        [Required(ErrorMessage = "角色名稱為必填項目")]
        [StringLength(256, ErrorMessage = "角色名稱長度不能超過 256 個字元")]
        public string RoleName { get; set; } = string.Empty;

        /// <summary>
        /// 角色範圍類型 (ScopeType)
        /// </summary>
        /// <example>LabScoped</example>
        [Display(Name = "範圍類型", Description = "請選擇角色生效邊界：SystemDefault(系統預設)、Global(全域)、LabScoped(實驗室專屬)")]
        public RoleScopeType ScopeType { get; set; } = default!;

        /// <summary>
        /// 管理階層數值 (HierarchyLevel)
        /// </summary>
        /// <example>30</example>
        [Display(Name = "階層數值 (HierarchyLevel)", Description = "決定管理權力大小。系統預設區間: 90-100，全域區間: 40-89，實驗室區間: 1-39。")]
        public int HierarchyLevel { get; set; } = default!;

        /// <summary>
        /// 所屬區域實驗室唯一識別碼
        /// </summary>
        /// <example>3fa85f64-5717-4562-b3fc-2c963f66afa6</example>
        [Display(Name = "所屬實驗室 ID", Description = "當 ScopeType 為 LabScoped 時為必填，其餘範圍請保持空白。")]
        public Guid? TenantLabId { get; set; } = default!;

        /// <summary>實驗室專屬角色適用的區域實驗室（可多選）；有值時優先於 TenantLabId</summary>
        public List<Guid>? TenantLabIds { get; set; }

        /// <summary>生效起日（含）；null 表示立即生效</summary>
        [Display(Name = "生效起日")]
        public DateTimeOffset? EffectiveFrom { get; set; }

        /// <summary>生效迄日（含）；null 表示無期限</summary>
        [Display(Name = "生效迄日")]
        public DateTimeOffset? EffectiveTo { get; set; }

        /// <summary>
        /// 角色描述 (Description)
        /// </summary>
        [Display(Name = "角色描述", Description = "描述角色的用途和功能。")]
        [StringLength(512, ErrorMessage = "角色描述長度不能超過 512 個字元")] 
        public string? Description { get; set; } = string.Empty;

        /// <summary>
        /// 是否為系統角色 (IsSystemRole)
        /// </summary>
        [Display(Name = "是否為系統角色", Description = "是否為系統角色。")]
        public bool IsSystemRole { get; set; } = false;
    }
}
