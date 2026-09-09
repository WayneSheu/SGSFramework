using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    public sealed class ForgotPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
    }
}
