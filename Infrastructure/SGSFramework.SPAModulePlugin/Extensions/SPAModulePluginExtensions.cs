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
    /// 註冊 SPA 外掛模組管裡、探索與實體磁碟儲存服務
    /// </summary>
    public static IServiceCollection AddSPAModulePlugin(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // 1. 註冊 SPA 模組探索與 Manifest 授權解析服務 (修復 IRequestHandler 注入失敗的問題)
        services.AddScoped<ISPAModuleDiscoveryService, SPAModuleDiscoveryService>();

        // 2. 註冊靜態檔案解壓縮與目錄管理服務
        services.AddScoped<ISPAModuleStorageService, SPAModuleStorageService>();

        return services;
    }
}