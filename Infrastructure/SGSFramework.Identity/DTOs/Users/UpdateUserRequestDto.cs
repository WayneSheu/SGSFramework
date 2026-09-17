using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.Identity.DTOs.Users
{
    /// <summary>
    /// 更新使用者資料請求資料傳輸物件 (DTO)
    /// </summary>
    public sealed record UpdateUserRequestDto
    {
        /// <summary>
        /// 使用者帳號名稱
        /// </summary>
        [Required(ErrorMessage = "使用者帳號為必填欄位。")]
        [StringLength(256, ErrorMessage = "使用者帳號長度不可超過 256 個字元。")]
        public string Username { get; init; } = string.Empty;

        /// <summary>
        /// 電子郵件地址
        /// </summary>
        [Required(ErrorMessage = "電子郵件為必填欄位。")]
        [EmailAddress(ErrorMessage = "電子郵件格式不正確。")]
        [StringLength(256, ErrorMessage = "電子郵件長度不可超過 256 個字元。")]
        public string Email { get; init; } = string.Empty;
    }
}
