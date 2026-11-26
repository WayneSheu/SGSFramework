using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGS.Modules.RUL.Application.Services;
using SGSFramework.Core.Rules;

namespace SGS.Modules.RUL.Application.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 RUL Application 層：規則維護／審批／測試服務，並以資料庫規則來源覆寫 Core 的 Null 預設
    /// （IRuleEngine、IRuleExpressionValidator、IRuleTester 由 Host 的 AddDynamicRuleEngine 提供）。
    /// </summary>
    public static IServiceCollection AddModuleRulApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();
        services.AddSingleton<RuleSnapshotProvider>();
        services.Replace(ServiceDescriptor.Singleton<IRuleSnapshotProvider>(sp => sp.GetRequiredService<RuleSnapshotProvider>()));
        services.AddScoped<RuleAdminService>();
        services.AddScoped<RuleTestService>();
        return services;
    }
}
