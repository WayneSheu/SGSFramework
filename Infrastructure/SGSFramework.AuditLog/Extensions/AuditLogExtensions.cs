// ==========================================
// 檔案路徑: Infrastructure/SGSFramework.AuditLog/Extensions/AuditLogExtensions.cs
// 架構層級: Infrastructure Layer (AuditLog Extension)
// ==========================================

namespace SGSFramework.AuditLog.Extensions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SGSFramework.AuditLog.Abstractions;
using SGSFramework.AuditLog.Channels;
using SGSFramework.AuditLog.Configurations;
using SGSFramework.AuditLog.Interceptors;
using SGSFramework.AuditLog.Services.Strategies;
using SGSFramework.AuditLog.Services.Worker;
using SGSFramework.Core.Abstractions.AuditLogs;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.HttpAuditProviders;
using System;

public static class AuditLogExtensions
{
    /// <summary>
    /// 註冊 AuditLog 基礎核心服務 (包含設定檔、HttpContext 身分提供者與兩大 SaveChanges 攔截器)
    /// </summary>
    public static IServiceCollection AddAuditLog(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<AuditOptions>(configuration.GetSection(AuditOptions.SectionName));
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditProvider, HttpAuditProvider>();

        // 必須為 Transient 確保每次 SaveChanges 解析全新實體
        services.AddTransient<AuditInterceptor>();
        services.AddTransient<PermissionAuditInterceptor>();

        return services;
    }

    /// <summary>
    /// 註冊特定模組的 DbContext、DbContextFactory、獨立泛型 AuditChannel、StorageStrategy 與專屬 AuditWorker
    /// </summary>
    /// <typeparam name="TContext">符合 IAuditDbContext 介面之模組 DbContext</typeparam>
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
            // 確保 Interceptors 已註冊
            services.AddTransient<AuditInterceptor>();
            services.AddTransient<PermissionAuditInterceptor>();

            var connectionString = ResolveConnectionString(configuration, connectionStringKey);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"無法找到有效的資料庫連線字串。搜尋 Key: '{connectionStringKey}'");
            }

            var moduleSchema = schemaName
                ?? typeof(TContext).Name.Replace("DbContext", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();

            // 1. 為此 TContext 註冊專屬的泛型 Channel 佇列 (Singleton)
            services.AddSingleton<IAuditChannel<TContext>, AuditChannel<TContext>>();

            // 2. 為此 TContext 註冊獨立的 Bulk 持久化儲存策略 (Singleton/Scoped 視實作而定)
            services.AddSingleton<IAuditStorageStrategy<TContext>, SqlBulkAuditStorageStrategy<TContext>>();

            // 3. 配置 DbContext 與 DbContextFactory 的選項 build 邏輯
            Action<IServiceProvider, DbContextOptionsBuilder> buildOptions = (sp, options) =>
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(TContext).Assembly.FullName);
                    sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", moduleSchema);
                    sqlOptions.CommandTimeout(180);
                });

                ConfigureAuditInterceptor(sp, options);
                configureOptions?.Invoke(options);
            };

            services.AddDbContextFactory<TContext>(buildOptions);
            services.AddDbContext<TContext>(buildOptions, ServiceLifetime.Scoped, ServiceLifetime.Singleton);

            // 4. 為此 TContext 註冊專屬的泛型背景消費服務 (HostedService)
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
    /// 解析連線字串優先順序：MigrationConnection -> DefaultConnection -> Configuration Direct Key
    /// </summary>
    private static string? ResolveConnectionString(IConfiguration configuration, string connectionStringKey)
    {
        var section = configuration.GetSection(connectionStringKey);
        var connectionString = section["MigrationConnection"] ?? section["DefaultConnection"];

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        connectionString = configuration.GetConnectionString(connectionStringKey) ?? configuration[connectionStringKey];

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        return configuration.GetConnectionString("DefaultConnection");
    }

    /// <summary>
    /// 為 DbContext 綁定 AuditInterceptor 與 PermissionAuditInterceptor 攔截器
    /// </summary>
    private static void ConfigureAuditInterceptor(IServiceProvider sp, DbContextOptionsBuilder options)
    {
        ArgumentNullException.ThrowIfNull(sp);
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            var auditInterceptor = sp.GetRequiredService<AuditInterceptor>();
            var permissionInterceptor = sp.GetRequiredService<PermissionAuditInterceptor>();

            options.AddInterceptors(auditInterceptor, permissionInterceptor);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[AuditLogExtensions] 解析 AuditInterceptor 時發生例外，將阻斷連線以確保資安規範。");
            throw;
        }
    }
}