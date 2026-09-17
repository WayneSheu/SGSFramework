namespace SGSFramework.Identity.Strategies;

using SGSFramework.Identity.Abstractions.Strategies;
using System;
using System.Collections.Generic;
using System.Linq;

public sealed class UserProvisioningStrategyFactory : IUserProvisioningStrategyFactory
{
    private readonly IEnumerable<IUserProvisioningStrategy> _strategies;

    public UserProvisioningStrategyFactory(IEnumerable<IUserProvisioningStrategy> strategies)
    {
        _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
    }

    public IUserProvisioningStrategy GetStrategy(UserProvisioningStrategyType strategyType)
    {
        // 將 Enum 轉為字串來比對實作中的 StrategyName (例如 "Atomic" 或 "EventDriven")
        string strategyName = strategyType.ToString();

        var strategy = _strategies.FirstOrDefault(s => string.Equals(s.StrategyName, strategyName, StringComparison.OrdinalIgnoreCase));
        if (strategy is null)
        {
            throw new KeyNotFoundException($"找不到對應的使用者配置策略: '{strategyName}'");
        }

        return strategy;
    }
}