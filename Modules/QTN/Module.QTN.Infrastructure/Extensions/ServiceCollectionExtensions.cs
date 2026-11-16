using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGS.Modules.QTN.Infrastructure.Dbcontexts;
using SGS.Modules.QTN.Infrastructure.Services;
using SGSFramework.AuditLog.Extensions;

namespace SGS.Modules.QTN.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Infrastructure 層：QTNDbContext（schema qtn，含資料異動稽核）與 Migration 服務
    /// </summary>
    public static IServiceCollection AddModuleQtnInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            services.AddModuleDatabaseWithAudit<QTNDbContext>(
                configuration,
                "PersistentSettings:ConnectionStrings",
                "qtn",
                dbContextOptionsBuilder =>
                {
                    var infrastructureAssembly = typeof(QTNDbContext).Assembly;
                    dbContextOptionsBuilder.UseSqlServer(sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly(infrastructureAssembly.FullName);
                        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "qtn");
                    });
                });

            // 以具體型別註冊（不註冊 IMigrationService，避免覆蓋其他模組）；由 ModuleQtnInitializer 自行解析
            services.AddScoped<QtnMigrationService>();
            return services;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("[QTN.Infrastructure] 資料庫與持久化服務註冊失敗", ex);
        }
    }
}
