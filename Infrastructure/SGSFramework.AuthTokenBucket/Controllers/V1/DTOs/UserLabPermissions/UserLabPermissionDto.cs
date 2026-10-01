using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.UserLabPermissions
{
    public sealed class UserLabPermissionDto
    {
        public string ControllerOrModuleKey { get; set; } = string.Empty;
        public long Bitmask { get; set; }
    }
}
