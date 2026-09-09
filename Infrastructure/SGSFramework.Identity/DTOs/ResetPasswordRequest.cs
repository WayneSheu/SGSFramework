using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    public sealed class ResetPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}
