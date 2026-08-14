using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SGSFramework.AuditLog.Channels;
using SGSFramework.AuditLog.Configurations;
using SGSFramework.AuditLog.Interceptors;
using SGSFramework.AuditLog.Services.Strategies;
using SGSFramework.AuditLog.Services.Worker;
using SGSFramework.Core.Abstractions.AuditLogs;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.HttpAuditProviders;
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
        services.AddTransient<AuditInterceptor>();

        return services;
    }

    /// <summary>
    /// 註冊特定模組的 DbContext、DbContextFactory、Audit 持久化單例策略與泛型 BackgroundService
    /// </summary>
    /// <typeparam name="TContext">實作 IAuditDbContext 介面的 DbContext 類型</typeparam>
    /// <param name="services">IServiceCollection</param>
    /// <param name="configuration">IConfiguration</param>
    /// <param name="connectionStringKey">連線字串 Key 或 Section 路徑</param>
    /// <param name="schemaName">資料庫 Schema 名稱（未傳入則預設自 Context 類別名稱截取）</param>
    /// <param name="configureOptions">額外的 DbContextOptionsBuilder 配置 Hook</param>
    public static IServiceCollection AddModuleDatabaseWithAudit<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringKey,
        string? schemaName = null,
        Action<DbContextOptionsBuilder>? configureOptions = null)
        where TContext : DbContext, IAuditDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionStringKey);

        try
        {
            // 1. 註冊 AuditInterceptor 為 Transient
            services.AddTransient<AuditInterceptor>();

            // 2. 解析連線字串 (完整 Search Chain 搜尋)
            var connectionString = ResolveConnectionString(configuration, connectionStringKey);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"無法找到有效的資料庫連線字串。搜尋 Key: '{connectionStringKey}'");
            }

            // 3. 自動推導 Schema 名稱 (例如 ORGDbContext -> org)
            var moduleSchema = schemaName
                ?? typeof(TContext).Name.Replace("DbContext", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();

            // 4. 註冊 Audit 儲存策略 (SqlBulkAuditStorageStrategy)
            services.AddSingleton<IAuditStorageStrategy<TContext>, SqlBulkAuditStorageStrategy<TContext>>();

            // 5. 核心：設定 Options 配置邏輯
            Action<IServiceProvider, DbContextOptionsBuilder> buildOptions = (sp, options) =>
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    // 明確指定 Migration Assembly 為 TContext 所在的 Assembly，避免 Plugin 動態載入時失聯
                    sqlOptions.MigrationsAssembly(typeof(TContext).Assembly.FullName);

                    // 精準隔離 Migration 歷史紀錄表至 [schema].[__EFMigrationsHistory]
                    sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", moduleSchema);

                    sqlOptions.CommandTimeout(180);
                });

                // 配置 AuditInterceptor 攔截器
                ConfigureAuditInterceptor(sp, options);

                // 執行外部傳入的客製化組態 Hook
                configureOptions?.Invoke(options);
            };

            // 6. 註冊 DbContextFactory (用於 Worker / 非同步背景任務)
            services.AddDbContextFactory<TContext>(buildOptions);

            // 7. 修正點：補齊標準 Scoped DbContext 註冊 (供 一般 Service / Controller 注入使用)
            services.AddDbContext<TContext>(buildOptions, ServiceLifetime.Scoped, ServiceLifetime.Singleton);

            // 8. 註冊背景稽核日誌 Worker
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
    /// 解析連線字串之輔助方法
    /// </summary>
    private static string? ResolveConnectionString(IConfiguration configuration, string connectionStringKey)
    {
        var section = configuration.GetSection(connectionStringKey);

        // 1. 嘗試直接從 Section 抓取 MigrationConnection 或 DefaultConnection
        var connectionString = section["MigrationConnection"] ?? section["DefaultConnection"];

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        // 2. 嘗試當作完整的 ConnectionString Key 或 Direct Path 讀取
        connectionString = configuration.GetConnectionString(connectionStringKey) ?? configuration[connectionStringKey];

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        // 3. 全局 Fallback：讀取預設連線字串
        return configuration.GetConnectionString("DefaultConnection");
    }

    /// <summary>
    /// 配置 AuditInterceptor 攔截器
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