using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Models
{
    /// <summary>
    /// 帳號修復請求 DTO
    /// </summary>
    public sealed class AccountRemediationRequest
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 補償驗證管道，如 "PasswordReset"、"MFA_Success" 等
        /// </summary>
        public string? VerificationMethod { get; set; }
    }
}
