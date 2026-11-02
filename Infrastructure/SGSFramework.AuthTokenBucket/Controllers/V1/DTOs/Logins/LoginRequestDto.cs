using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.Logins
{
    /// <summary>
    /// 帳密登入請求 DTO
    /// </summary>
    public sealed class LoginRequestDto
    {
        /// <summary>
        /// 使用者帳號或電子郵件（選填，若未填可透過 EmployeeNo 登入）
        /// </summary>
        public string? NameOrEmail { get; set; }

        /// <summary>
        /// 員工編號（選填，若未填可透過 NameOrEmail 登入）
        /// </summary>
        public string? EmployeeNo { get; set; }

        /// <summary>
        /// 密碼
        /// </summary>
        [Required(ErrorMessage = "密碼為必填項。")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// 指定請求登入的實驗室識別碼（選填）
        /// </summary>
        public string? RequestedLabId { get; set; }
    }
}
