using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Enterprise.HttpSdk.Dtos
{
    /// <summary>
    /// Change password request DTO.
    /// </summary>
    public sealed class ChangePasswordRequest
    {
        [Required(ErrorMessage = "請輸入目前密碼")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入新密碼")]
        [StringLength(100, ErrorMessage = "密碼長度至少需要 {2} 個字元。", MinimumLength = 8)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "請再次確認新密碼")]
        [Compare("NewPassword", ErrorMessage = "新密碼與確認密碼不相符。")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
