using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.DataProtection.Configurations
{
    public class StrategyConfig
    {
        public string KeyAlias { get; set; } = string.Empty;
        public int KeyRotationDays { get; set; } = 90;
        public bool UseHms { get; set; } = true;
    }
}
