using System;
using System.Collections.Generic;
using System.Text;

#nullable enable
namespace SGSFramework.AuthTokenBucket.DTOs.UserLabPermissions
{
    public class AssignUserLabPermissionRequest
    {
        public int LabId { get; set; }
        public string ControllerOrModuleKey { get; set; } = string.Empty;
        public long Bitmask { get; set; }
    }
}
