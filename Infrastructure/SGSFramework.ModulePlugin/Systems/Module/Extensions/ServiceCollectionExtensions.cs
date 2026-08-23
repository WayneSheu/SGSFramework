using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGSFramework.ModulePlugin.Abstractions;
using SGSFramework.ModulePlugin.Systems.Module.Repositories;
using SGSFramework.ModulePlugin.Systems.Module.Services;
using SGSFramework.ModulePlugin.Systems.Module.Strategies;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ModulePlugin.Systems.Module.Extensions
{
    /// <summary>
    /// 提供 IServiceCollection 擴展方法，用於註冊模組框架相關的服務
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 註冊外掛模組框架之資料持久化與檔案處理解決方案
        /// </summary>
        public static IServiceCollection AddModuleFrameworkServices<TDbContext>(this IServiceCollection services)
            where TDbContext : DbContext
        {
            ArgumentNullException.ThrowIfNull(services);

            // 註冊外掛檔案實體管理服務
            services.TryAddScoped<IPluginFileStorageService, PluginFileStorageService>();

            // 註冊泛型 EF Core 存取策略與 Repository
            services.TryAddScoped<IModuleStorageStrategy<TDbContext>, EfCoreModuleStorageStrategy<TDbContext>>();
            services.TryAddScoped<IModuleRepository, ModuleRepository<TDbContext>>();

            return services;
        }
    }
}
