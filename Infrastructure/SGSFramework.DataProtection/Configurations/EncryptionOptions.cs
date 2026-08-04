using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.DataProtection.Configurations
{
    /// <summary>
    /// 加密策略通用設定
    /// </summary>
    public class EncryptionOptions
    {
        public const string SectionName = "EncryptionSettings";
        public string DefaultStrategy { get; set; } = "AES-256-GCM";
        public Dictionary<string, StrategyConfig> Strategies { get; set; } = new();
    }
}
