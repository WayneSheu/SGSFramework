// ==========================================
// 檔案路徑: Infrastructure/SGSFramework.AuditLog/Extensions/AuditLogExtensions.cs
// 架構層級: Infrastructure Layer (AuditLog Extension)
// ==========================================

namespace SGSFramework.AuditLog.Extensions;

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

        // 註冊兩個 Audit 攔截器
        services.AddTransient<AuditInterceptor>();
        services.AddTransient<PermissionAuditInterceptor>();

        return services;
    }

    /// <summary>
    /// 註冊特定模組的 DbContext、DbContextFactory、Audit 持久化單例策略與泛型 BackgroundService
    /// </summary>
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
            services.AddTransient<AuditInterceptor>();
            services.AddTransient<PermissionAuditInterceptor>();

            var connectionString = ResolveConnectionString(configuration, connectionStringKey);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"無法找到有效的資料庫連線字串。搜尋 Key: '{connectionStringKey}'");
            }

            var moduleSchema = schemaName
                ?? typeof(TContext).Name.Replace("DbContext", "", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();

            services.AddSingleton<IAuditStorageStrategy<TContext>, SqlBulkAuditStorageStrategy<TContext>>();

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
            services.AddHostedService<AuditWorker<TContext>>();

            return services;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[AuditLogExtensions] 配置 {DbContextName} 資料庫與稽核攔截器失敗。", typeof(TContext).Name);
            throw;
        }
    }

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

    private static void ConfigureAuditInterceptor(IServiceProvider sp, DbContextOptionsBuilder options)
    {
        ArgumentNullException.ThrowIfNull(sp);
        ArgumentNullException.ThrowIfNull(options);

        try
        {
            var auditInterceptor = sp.GetService<AuditInterceptor>();
            var permissionInterceptor = sp.GetService<PermissionAuditInterceptor>();

            if (auditInterceptor != null && permissionInterceptor != null)
            {
                options.AddInterceptors(auditInterceptor, permissionInterceptor);
            }
            else if (auditInterceptor != null)
            {
                options.AddInterceptors(auditInterceptor);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[AuditLogExtensions] 解析 AuditInterceptor 時發生例外，DbContext 將以無稽核攔截模式運作。");
        }
    }
}