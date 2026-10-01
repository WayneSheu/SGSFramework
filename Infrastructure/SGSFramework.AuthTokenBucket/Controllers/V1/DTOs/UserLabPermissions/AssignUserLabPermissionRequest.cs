using System;
using System.Collections.Generic;
using System.Text;

#nullable enable
namespace SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.UserLabPermissions
{
    public class AssignUserLabPermissionRequest
    {
        public int LabId { get; set; }
        public string ControllerOrModuleKey { get; set; } = string.Empty;
        public long Bitmask { get; set; }
    }
}
