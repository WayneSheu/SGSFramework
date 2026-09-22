using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs.RolePermissions
{
    public class UpdateRolePermissionsRequest
    {
        public string RoleId { get; set; } = string.Empty;
        public List<string> PermissionKeys { get; set; } = new();
    }
}
