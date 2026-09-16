

namespace SGSFramework.Identity.Strategies;

using System;
using System.Collections.Generic;
using System.Linq;
using SGSFramework.Identity.Abstractions.Strategies;

public sealed class UserProvisioningStrategyFactory : IUserProvisioningStrategyFactory
{
    private readonly IEnumerable<IUserProvisioningStrategy> _strategies;

    public UserProvisioningStrategyFactory(IEnumerable<IUserProvisioningStrategy> strategies)
    {
        _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
    }

    public IUserProvisioningStrategy GetStrategy(string strategyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyName);

        var strategy = _strategies.FirstOrDefault(s => string.Equals(s.StrategyName, strategyName, StringComparison.OrdinalIgnoreCase));
        if (strategy is null)
        {
            throw new KeyNotFoundException($"找不到對應的使用者配置策略: '{strategyName}'");
        }

        return strategy;
    }
}