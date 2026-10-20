using Azure.Core;
using SGSFramework.Core.Abstractions.Entities.Identities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    public sealed class UpdateRoleRequest
    {
        [Required(ErrorMessage = "角色 ID 為必填項目")]
        public string RoleId { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public RoleScopeType ScopeType  { get; set; } = default!;
        
        public int HierarchyLevel { get; set; } = default!;
        public Guid? TenantLabId  { get; set; } = default!;

        /// <summary>生效起日（含）；null 表示立即生效</summary>
        public DateTimeOffset? EffectiveFrom { get; set; }

        /// <summary>生效迄日（含）；null 表示無期限</summary>
        public DateTimeOffset? EffectiveTo { get; set; }


        [Required(ErrorMessage = "角色名稱為必填項目")]
        [StringLength(256, ErrorMessage = "角色名稱長度不能超過 256 個字元")]
        public string NewRoleName { get; set; } = string.Empty;




        public string Description { get; set; } = string.Empty;
    }
}
