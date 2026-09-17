using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.Options
{
    /// <summary>
    /// 使用者配置策略選項設定 (對應 appsettings.json)
    /// </summary>
    public sealed class UserProvisioningOptions
    {
        public const string SectionName = "UserProvisioning";

        /// <summary>
        /// 目前系統啟用的使用者配置策略類型 (例如: Atomic, Batch, LDAP)
        /// </summary>
        public string StrategyType { get; set; } = "Atomic";
    }
}
