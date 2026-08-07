using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    public sealed class CreateRoleRequest
    {
        [Required(ErrorMessage = "角色名稱為必填項目")]
        [StringLength(256, ErrorMessage = "角色名稱長度不能超過 256 個字元")]
        public string RoleName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
    }
}
