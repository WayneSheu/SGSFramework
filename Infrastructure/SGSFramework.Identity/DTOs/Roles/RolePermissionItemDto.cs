using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs.Roles
{
    public class RolePermissionItemDto
    {
        public string ModuleName { get; set; } = string.Empty;
        public string ModuleTitle { get; set; } = string.Empty;
        public string FunctionName { get; set; } = string.Empty;
        public string FunctionTitle { get; set; } = string.Empty;
        public string PermissionKey { get; set; } = string.Empty;
        public string PermissionTitle { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
