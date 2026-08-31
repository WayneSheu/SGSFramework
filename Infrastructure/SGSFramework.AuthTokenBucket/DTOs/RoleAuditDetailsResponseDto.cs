using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs
{
    public sealed class RoleAuditDetailsResponseDto
    {
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public List<RoleMemberDto> Members { get; set; } = new();
        public object? Permissions { get; set; }
    }
}
