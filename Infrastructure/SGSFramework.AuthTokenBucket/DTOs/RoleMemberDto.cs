using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs
{
    public sealed class RoleMemberDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }
}
