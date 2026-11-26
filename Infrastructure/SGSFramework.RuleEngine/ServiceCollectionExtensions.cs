using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGSFramework.Core.Rules;

namespace SGSFramework.RuleEngine;

public static class RuleEngineServiceCollectionExtensions
{
    /// <summary>註冊引擎與運算式驗證；規則來源由 RUL 外掛以 IRuleSnapshotProvider 覆寫，未安裝時為 Null 實作。</summary>
    public static IServiceCollection AddDynamicRuleEngine(this IServiceCollection services)
    {
        services.TryAddSingleton<IRuleSnapshotProvider, NullRuleSnapshotProvider>();
        services.AddRuleTarget<SampleRuleTarget>("示範目標（試跑用）");
        services.TryAddSingleton<IRuleTargetCatalog, RuleTargetCatalog>();
        services.TryAddSingleton<IRuleTester, RuleTester>();
        services.TryAddSingleton<IRuleExpressionValidator>(sp => new RuleExpressionValidator(sp.GetRequiredService<IRuleTargetCatalog>()));
        services.TryAddScoped<IRuleEngine, DynamicRuleEngine>();
        return services;
    }
}
