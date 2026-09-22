using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs.RolePermissions
{
    public class RolePermissionMatrixDto
    {
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public List<string> GrantedPermissionKeys { get; set; } = new();
        public List<int> GrantedBitPositions { get; set; } = new();
    }
}
