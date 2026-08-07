using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    public sealed class AssignUserRolesRequest
    {
        [Required(ErrorMessage = "使用者 ID 為必填項目")]
        public string UserId { get; set; } = string.Empty;

        public List<string> RoleNames { get; set; } = new();
    }
}
