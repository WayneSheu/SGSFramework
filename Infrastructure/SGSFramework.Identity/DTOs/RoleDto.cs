using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    public sealed class RoleDto
    {
        public string Id { get; set; } = string.Empty;     
        public string Name { get; set; } = string.Empty;
        //public string NormalizedName { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
        public List<string> MappedAdGroups { get; set; } = new();
    }
}
