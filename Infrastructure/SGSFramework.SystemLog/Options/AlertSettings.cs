using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.Options
{
    public sealed class AlertSettings
    {
        public bool IsEnabled { get; set; } = true;
        public int CooldownSeconds { get; set; } = 60;
        public SmtpConfig Smtp { get; set; } = new();
    }
}
