using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    public sealed class RemoveAdGroupFromRoleRequest
    {
        [Required(ErrorMessage = "角色 ID 為必填項目")]
        public string RoleId { get; set; } = string.Empty;

        [Required(ErrorMessage = "AD 群組名稱為必填項目")]
        public string AdGroupName { get; set; } = string.Empty;
    }
}
