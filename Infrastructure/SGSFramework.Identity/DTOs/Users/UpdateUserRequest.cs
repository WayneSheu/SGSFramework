using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.Identity.DTOs.Users
{
    /// <summary> 
    /// 更新使用者基本資料請求 DTO 
    /// </summary> 
    public sealed record UpdateUserRequest
    {
        [Required(ErrorMessage = "電子郵件為必填欄位")]
        [EmailAddress(ErrorMessage = "請輸入有效的電子郵件格式")]
        public string Email { get; init; } = string.Empty;

        public bool IsActive { get; init; }
        public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
        public Guid? PrimaryLabId { get; init; }
    }
}
