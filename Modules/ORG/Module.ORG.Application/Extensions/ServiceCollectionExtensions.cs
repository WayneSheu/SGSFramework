using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGS.Modules.ORG.Application.Abstractions;
using SGS.Modules.ORG.Application.Services;
using SGS.Modules.ORG.Infrastructure.Services;
using SGSFramework.AuthTokenBucket.RuleEngine.Abstractions;
using SGSFramework.AuthTokenBucket.RuleEngine.Rules.Laboratory;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Permissions.Contract;
using System;
using System.Reflection;

namespace SGS.Modules.ORG.Application.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Application 層之核心服務、跨模組適配器與 MediatR 管線
    /// </summary>
    public static IServiceCollection AddModuleOrgApplication(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            // 1. 註冊 Application 領域業務服務
            services.TryAddScoped<IOrganizationService, OrganizationService>();

            // 2. 註冊跨模組整合服務 (防腐層適配器 - 位於 Application 層)
            services.AddScoped<IOrganizationIntegrationService, OrganizationIntegrationService>();

            // 3. 自動掃描並註冊 Application Assembly 內所有 MediatR Handlers / Requests / Pipeline Behaviors
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            });

            // 自動註冊 AuthTokenBucket 內的規則引擎與所有規則實作
            services.AddScoped<LaboratoryRuleEngine>();

            var ruleInterfaceType = typeof(ILabAuthorizationRule);
            var ruleImplementations = typeof(LaboratoryCategoryRule).Assembly
                .GetTypes()
                .Where(t => ruleInterfaceType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var ruleType in ruleImplementations)
            {
                services.AddScoped(ruleInterfaceType, ruleType);
            }

            return services;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("[ORG.Application] 依賴服務註冊失敗", ex);
        }
    }
}