using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.Identity.DTOs.Users
{
    /// <summary>
    /// 電子郵件確認請求資料模型
    /// </summary>
    public sealed record ConfirmEmailRequest(
        [Required(ErrorMessage = "使用者識別碼不得為空。")]
    string UserId,

        [Required(ErrorMessage = "驗證權杖不得為空。")]
    string Token
    );
}
