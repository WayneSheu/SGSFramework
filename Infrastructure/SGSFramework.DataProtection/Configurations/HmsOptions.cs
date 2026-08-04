using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.DataProtection.Configurations
{
    public class HmsOptions
    {
        public const string SectionName = "HmsSettings";
        public string SharedSecret { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
    }
}
