using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGS.Modules.LOG.Infrastructure.Dbcontexts;
using SGS.Modules.LOG.Infrastructure.Services;
using SGSFramework.Core.Logging;
using SGSFramework.Core.Scheduling;

namespace SGS.Modules.LOG.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Infrastructure 層：LogDbContext（獨立日誌庫，連線取自 LogConnection）與 Migration 服務。
    /// 呼叫前須確認 <see cref="LogDatabaseOptions.IsConfigured"/>。
    /// </summary>
    public static IServiceCollection AddModuleLogInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration[LogDatabaseOptions.RuntimeConnectionKey];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"未設定日誌資料庫連線：{LogDatabaseOptions.RuntimeConnectionKey}");
        }

        void Configure(DbContextOptionsBuilder options) =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(LogDbContext).Assembly.FullName);
                sql.MigrationsHistoryTable("__EFMigrationsHistory", "log");
            });

        // Singleton 工廠（供 Singleton store 取短命 DbContext）；不可再 AddDbContext（會讓 DbContextOptions 變 Scoped，
        // 導致 Singleton 工廠解析失敗而使 Host 無法啟動）。Scoped 的 LogDbContext 由同一工廠建立，Scope 結束時一併釋放。
        services.AddDbContextFactory<LogDbContext>(Configure);
        services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<LogDbContext>>().CreateDbContext());

        // 冷熱分層：熱庫 + LogArchive:Databases 設定的唯讀冷庫（年度換庫後的歷史庫）
        services.AddSingleton<ILogTierCatalog>(_ => new LogTierCatalog(configuration));
        services.AddSingleton<LogColdContextFactory>();

        // 年度換庫（半自動）：換庫專用帳號 RotationConnection（rotate_sgs_user）；未設定時僅無法換庫，不影響日誌寫入與查詢
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<LogRotationService>();

        // 以具體型別註冊（不註冊 IMigrationService，避免覆蓋其他模組）
        services.AddScoped<LogMigrationService>();
        // 首次啟動自動建庫 / 建立使用者（套用 Migration 前執行）
        services.AddTransient<LogDatabaseProvisioner>();

        // 獨立日誌庫不在主庫備份範圍：宣告為模組備份目標，由排程模組產生「日誌資料庫完整 / 差異備份」作業
        // （主庫完整備份 02:00，日誌庫錯開至 02:30；備份帳號 backup_sgs_user 須為 PhysLIMSLogDB 的 db_backupoperator）
        services.AddSingleton(new ModuleDatabaseBackupTarget("log", "日誌資料庫", LogDatabaseOptions.RuntimeConnectionKey));
        return services;
    }
}
