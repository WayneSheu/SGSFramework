// Path: src/SGSFramework/Host/PhysLIMS.Controller/DependencyInjection/SystemConfigurationExtensions.cs
namespace SGSFramework.ApiInfrastructure.DependencyInjection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.ApiInfrastructure.Configurations;
using SGSFramework.Core.Configurations;
using System;

/// <summary>
/// 系統配置管理：集中註冊 Host 層與跨模組共用的強型別 Options。
/// 各 Infrastructure 模組自有的 Options (AuditSettings、SeedAdmin、ModularSettings...) 仍由各模組擴充方法自行註冊。
/// </summary>
public static class SystemConfigurationExtensions
{
    /// <summary>
    /// 註冊系統層級強型別組態，全部啟用 DataAnnotations 與啟動時驗證 (ValidateOnStart)，
    /// 組態錯誤會在 app 啟動時立即失敗，而非延遲到第一次使用時才發生。
    /// </summary>
    public static IServiceCollection AddSystemConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddValidatedOptions<DeploymentOptions>(DeploymentOptions.SectionName);
        services.AddValidatedOptions<CorsSettingsOptions>(CorsSettingsOptions.SectionName)
            .Validate(o => o.AllowedOrigins.All(origin => !string.IsNullOrWhiteSpace(origin)),
                "CorsSettings:AllowedOrigins 不可包含空白項目。");
        services.AddValidatedOptions<DatabaseStartupOptions>(DatabaseStartupOptions.SectionName);
        services.AddValidatedOptions<IisHostingOptions>(IisHostingOptions.SectionName);
        services.AddValidatedOptions<PersistentConnectionOptions>(PersistentConnectionOptions.SectionName);

        return services;
    }
}
