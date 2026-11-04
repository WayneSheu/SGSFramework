using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGS.Modules.SCH.Infrastructure.Dbcontexts;
using SGS.Modules.SCH.Infrastructure.Services;
using SGSFramework.AuditLog.Extensions;

namespace SGS.Modules.SCH.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Infrastructure 層：SCHDbContext（schema sch，含資料異動稽核）與 Migration 服務
    /// </summary>
    public static IServiceCollection AddModuleSchInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            services.AddModuleDatabaseWithAudit<SCHDbContext>(
                configuration,
                "PersistentSettings:ConnectionStrings",
                "sch",
                dbContextOptionsBuilder =>
                {
                    var infrastructureAssembly = typeof(SCHDbContext).Assembly;
                    dbContextOptionsBuilder.UseSqlServer(sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly(infrastructureAssembly.FullName);
                        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "sch");
                    });
                });

            // 以具體型別註冊（不註冊 IMigrationService，避免覆蓋其他模組）；由 ModuleSchInitializer 自行解析
            services.AddScoped<SchMigrationService>();
            return services;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("[SCH.Infrastructure] 資料庫與持久化服務註冊失敗", ex);
        }
    }
}
