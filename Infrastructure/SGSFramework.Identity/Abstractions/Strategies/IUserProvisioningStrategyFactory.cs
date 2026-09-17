using SGSFramework.Identity.Abstractions.Strategies;

public interface IUserProvisioningStrategyFactory
{
    /// <summary>
    /// 依據策略列舉取得對應的實作策略
    /// </summary>
    IUserProvisioningStrategy GetStrategy(UserProvisioningStrategyType strategyType);
}