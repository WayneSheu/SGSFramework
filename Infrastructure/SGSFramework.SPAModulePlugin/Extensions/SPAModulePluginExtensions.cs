namespace SGSFramework.SPAModulePlugin.Extensions;

using Microsoft.Extensions.DependencyInjection;
using SGSFramework.SPAModulePlugin.Application.Abstractions;
using SGSFramework.SPAModulePlugin.Domain.Abstractions;
using SGSFramework.SPAModulePlugin.Domain.Repositories;
using SGSFramework.SPAModulePlugin.Infrastructure.Persistence;
using SGSFramework.SPAModulePlugin.Infrastructure.Security;
using SGSFramework.SPAModulePlugin.Infrastructure.Services;

public static class SPAModulePluginExtensions
{
    /// <summary>
    /// 註冊 SGSFramework SPA 動態外掛框架服務
    /// </summary>
    public static IServiceCollection AddSPAModulePlugin(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISPAModuleSecurityVerifier, SPAModuleSecurityVerifier>();
        services.AddScoped<ISPAModuleRepository, SPAModuleRepository>();
        services.AddScoped<ISPAModuleDiscoveryService, SPAModuleDiscoveryService>();

        return services;
    }
}