using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    public sealed class AssignUserPermissionsRequest
    {
        public List<string> Permissions { get; set; } = new();
    }
}
