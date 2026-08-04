using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.Options
{
    public sealed class SmtpConfig
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 25;
        public bool EnableSsl { get; set; } = true;
        public string FromAddress { get; set; } = string.Empty;
        public string ToAddresSGSFramework { get; set; } = string.Empty;

        // 🔐 新增認證欄位 (若為空則自動退回匿名 Relay 模式)
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}
