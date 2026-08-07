using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs
{
    public class PermissionModuleDto
    {
        public string ModuleName { get; set; } = string.Empty;
        public List<PermissionControllerDto> Controllers { get; set; } = new();
    }

    public class PermissionControllerDto
    {
        public string ControllerName { get; set; } = string.Empty;
        public List<PermissionItemDto> Permissions { get; set; } = new();
    }

    public class PermissionItemDto
    {
        public int Id { get; set; }
        public string PermissionKey { get; set; } = string.Empty;
        public int BitPosition { get; set; }
        public string ActionName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public class RolePermissionMatrixDto
    {
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public List<string> GrantedPermissionKeys { get; set; } = new();
        public List<int> GrantedBitPositions { get; set; } = new();
    }

    public class UpdateRolePermissionsRequest
    {
        public string RoleId { get; set; } = string.Empty;
        public List<string> PermissionKeys { get; set; } = new();
    }
}
