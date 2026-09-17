using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.Identity.DTOs.Users
{
    /// <summary>
    /// 註冊請求 DTO，包含使用者名稱、電子郵件與密碼
    /// </summary>
    public sealed class CreateUserRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "請指定預設角色")]
        public string DefaultRole { get; init; } = string.Empty;

        [Required(ErrorMessage = "請指定所屬實驗室維度 ID")]
        public Guid PrimaryLabId { get; init; }
    }
}
