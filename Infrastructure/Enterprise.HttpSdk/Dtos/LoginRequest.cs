using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Enterprise.HttpSdk.Dtos
{
    /// <summary>
    /// 企業級安全登入請求資料傳輸物件
    /// </summary>
    public sealed class LoginRequest
    {
        [Required(ErrorMessage = "請輸入使用者帳號或電子郵件")]
        [StringLength(50, ErrorMessage = "帳號長度不得超過 50 個字元")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "請輸入密碼")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
