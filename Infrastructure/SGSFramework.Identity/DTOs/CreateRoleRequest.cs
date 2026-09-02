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

        [StringLength(512, ErrorMessage = "角色描述長度不能超過 512 個字元")] 
        public string Description { get; set; } = string.Empty;
    }
}
