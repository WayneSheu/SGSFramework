using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SGSFramework.Identity.Abstractions.Strategies
{
    /// <summary>
    /// 使用者配置策略類型列舉
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum UserProvisioningStrategyType
    {
        /// <summary>
        /// 原子化同步寫入策略
        /// </summary>
        Atomic = 1,

        /// <summary>
        /// 事件驅動非同步處理策略
        /// </summary>
        EventDriven = 2
    }
}
