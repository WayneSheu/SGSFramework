using SGSFramework.Core.Results;
using SGSFramework.Identity.DTOs.Strategies;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.Abstractions.Strategies
{
    /// <summary>
    /// 使用者配置策略介面 (Strategy Pattern)
    /// </summary>
    public interface IUserProvisioningStrategy
    {
        /// <summary>
        /// 策略識別名稱 ("Atomic" 或 "EventDriven")
        /// </summary>
        string StrategyName { get; }

        /// <summary>
        /// 執行使用者建立與初始化配置
        /// </summary>
        Task<Result<Guid>> ProvisionUserAsync(UserProvisioningContext context, CancellationToken cancellationToken = default);
    }
}
