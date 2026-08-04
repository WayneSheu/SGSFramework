using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Models
{
    /// <summary>
    /// 安全權杖刷新與註銷請求的入參 DTO
    /// </summary>
    public sealed class LocalRefreshRequest
    {
        /// <summary>
        /// 使用者唯一識別碼（如 wayne_dev）
        /// </summary>
        [Required(ErrorMessage = "使用者識別碼 (UserId) 為必填欄位。")]
        public string UserId { get; set; } = string.Empty;

        // 選填：自主登出時帶上增加資安強度；遠端管理登出某一特定裝置時可不帶
        public string? OldRefreshToken { get; set; }

        //允許顯示指定要登出的目標裝置 ID
        public string? TargetDeviceId { get; set; }


    }
}
