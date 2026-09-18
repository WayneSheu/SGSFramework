using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs.Users
{
    /// <summary>
    /// 忘記密碼回應 DTO
    /// </summary>
    public sealed class ForgotPasswordResponse
    {
        /// <summary>
        /// 回應訊息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 除錯用重設密碼權杖 (正式環境應關閉或僅供內部測試)
        /// </summary>
        public string? DebugResetToken { get; set; }
    }
}
