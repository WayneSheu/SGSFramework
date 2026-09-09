using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    public sealed class AssignUserRolesRequest
    {
        /// <summary>
        /// 欲指派的角色名稱清單
        /// </summary>
        public List<string> RoleNames { get; set; } = [];
    }
}
