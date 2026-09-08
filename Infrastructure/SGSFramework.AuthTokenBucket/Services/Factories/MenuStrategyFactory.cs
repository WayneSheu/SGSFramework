namespace SGSFramework.AuthTokenBucket.Services.Factories;

using System;
using System.Collections.Generic;
using System.Linq;
using SGSFramework.Core.Abstractions.Menus;

/// <summary>
/// 選單解析策略工廠實作
/// </summary>
public class MenuStrategyFactory : IMenuStrategyFactory
{
    private readonly IEnumerable<IMenuResolutionStrategy> _strategies;

    public MenuStrategyFactory(IEnumerable<IMenuResolutionStrategy> strategies)
    {
        _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
    }

    public IMenuResolutionStrategy GetStrategy(MenuStrategyType strategyType = MenuStrategyType.DatabaseDriven)
    {
        var strategy = _strategies.FirstOrDefault(s => s.StrategyType == strategyType);
        return strategy ?? throw new NotSupportedException($"未註冊對應之選單解析策略: {strategyType}");
    }
}