using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs.UserLabPermissions
{
    public sealed class UserLabPermissionDto
    {
        public string ControllerOrModuleKey { get; set; } = string.Empty;
        public long Bitmask { get; set; }
    }
}
