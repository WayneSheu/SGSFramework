using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Models
{
    /// <summary>
    /// 密碼重置票證模型
    /// 重設密碼憑證（Reset Token）的短效性與狀態化
    /// </summary>
    public sealed class PasswordResetTicket
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string TicketId { get; set; } = string.Empty; // 強隨機加密字串 (Crypto-Random)
        public DateTime ExpiresAt { get; set; }
        public bool IsUsed { get; set; }
        public string? GeneratedFromIp { get; set; }

    }
}
