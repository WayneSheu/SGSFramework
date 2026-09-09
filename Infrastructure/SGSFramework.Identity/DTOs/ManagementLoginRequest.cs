using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    /// <summary>
    /// 登入請求 DTO，包含帳號識別碼與密碼
    /// </summary>
    public sealed class ManagementLoginRequest
    {
        public string AccountIdentifier { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
