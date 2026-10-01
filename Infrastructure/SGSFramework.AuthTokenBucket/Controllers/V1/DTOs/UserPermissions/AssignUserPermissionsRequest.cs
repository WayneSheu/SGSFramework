using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.UserPermissions
{
    public sealed class AssignUserPermissionsRequest
    {
        public List<string> Permissions { get; set; } = new();
    }
}
