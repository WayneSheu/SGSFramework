using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    /// <summary>
    /// 註冊請求 DTO，包含使用者名稱、電子郵件與密碼
    /// </summary>
    public sealed class RegisterRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
