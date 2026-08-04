using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Models
{
    /// <summary>
    /// 帳號遭竊或安全風險事件主動熔斷請求的入參 DTO
    /// </summary>
    public sealed class AccountRiskRequest
    {
        /// <summary>
        /// 遭遇安全性風險、需即時實施主動防禦隔離的使用者唯一識別碼
        /// </summary>
        [Required(ErrorMessage = "必須提供遭遇風險之使用者識別碼 (UserId)。")]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 觸發此風險熔斷的核心原因（例如：使用者主動通報憑證洩漏、地理位置異常跳躍、非法 Token 重放等）
        /// </summary>
        public string? Reason { get; set; }
    }
}
