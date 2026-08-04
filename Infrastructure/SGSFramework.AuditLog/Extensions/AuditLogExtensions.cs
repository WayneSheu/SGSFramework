using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SGSFramework.Core.Abstractions.AuditLogs;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.HttpAuditProviders;
using SGSFramework.AuditLog.Channels;
using SGSFramework.AuditLog.Configurations;
using SGSFramework.AuditLog.Interceptors;
using SGSFramework.AuditLog.Services.Strategies;
using SGSFramework.AuditLog.Services.Worker;
using System;

namespace SGSFramework.AuditLog.Extensions;

public static class AuditLogExtensions
{
    public static IServiceCollection AddAuditLog(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AuditOptions>(configuration.GetSection(AuditOptions.SectionName));
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditProvider, HttpAuditProvider>();
        services.AddSingleton<AuditChannel>();

        // 🚀 關鍵修正 1：統一改為 Transient，防止其在 AddAuditLog 與 AddModuleDatabaseWithAudit 中生命週期不一致
        services.AddTransient<AuditInterceptor>();

        return services;
    }

    /// <summary>
    /// 註冊特定模組的 DbContext Factory、Audit 持久化單例策略與泛型 BackgroundService
    /// </summary>
    public static IServiceCollection AddModuleDatabaseWithAudit<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringKey)
        where TContext : DbContext, IAuditDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringKey);

        try
        {
            // 確保 AuditInterceptor 為 Transient
            services.AddTransient<AuditInterceptor>();

            // 解析連線字串 (支援 Full Key Path 與 DefaultConnection 降級機制)
            string? connectionString = configuration[connectionStringKey]
                ?? configuration.GetConnectionString(connectionStringKey)
                ?? configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"無法找到有效的連線字串。尋找 Key: '{connectionStringKey}'");
            }

            // 將持久化策略註冊為 Singleton，符合 IHostedService 單例注入規範
            services.AddSingleton<IAuditStorageStrategy<TContext>, SqlBulkAuditStorageStrategy<TContext>>();

            // 🚀 關鍵修正 2：移除 services.AddDbContext<TContext>()！
            // AddDbContextFactory 會自動將 IDbContextFactory 註冊為 Singleton、DbContextOptions 註冊為 Singleton，
            // 並同時將 TContext 註冊為 Scoped (使用 Factory 建立實例)，徹底解決生命週期衝突的問題。
            services.AddDbContextFactory<TContext>((sp, options) =>
            {
                options.UseSqlServer(connectionString);
                ConfigureAuditInterceptor(sp, options);
            });

            // 註冊單例背景服務
            services.AddHostedService<AuditWorker<TContext>>();

            return services;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[AuditLogExtensions] 配置 {DbContextName} 資料庫與稽核攔截器失敗。", typeof(TContext).Name);
            throw;
        }
    }

    /// <summary>
    /// 安全解析 AuditInterceptor 並掛載至 DbContextOptionsBuilder
    /// </summary>
    private static void ConfigureAuditInterceptor(IServiceProvider sp, DbContextOptionsBuilder options)
    {
        ArgumentNullException.ThrowIfNull(sp);
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            var interceptor = sp.GetService<AuditInterceptor>();
            if (interceptor != null)
            {
                options.AddInterceptors(interceptor);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[AuditLogExtensions] 解析 AuditInterceptor 時發生例外，DbContext 將以無稽核攔截模式運作。");
        }
    }
}