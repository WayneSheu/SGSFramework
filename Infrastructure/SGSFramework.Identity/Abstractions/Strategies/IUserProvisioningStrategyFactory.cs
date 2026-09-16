using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.Abstractions.Strategies
{
    /// <summary>
    /// 使用者配置策略工廠介面
    /// </summary>
    public interface IUserProvisioningStrategyFactory
    {
        /// <summary>
        /// 依據名稱取得對應的配置策略實作
        /// </summary>
        IUserProvisioningStrategy GetStrategy(string strategyName);
    }
}
