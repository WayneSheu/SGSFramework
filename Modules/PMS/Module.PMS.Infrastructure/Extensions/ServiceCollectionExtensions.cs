using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Services;
using SGSFramework.AuditLog.Extensions;

namespace SGS.Modules.PMS.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Infrastructure 層：PMSDbContext（schema pms，含資料異動稽核）與 Migration 服務
    /// </summary>
    public static IServiceCollection AddModulePmsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            services.AddModuleDatabaseWithAudit<PMSDbContext>(
                configuration,
                "PersistentSettings:ConnectionStrings",
                "pms",
                dbContextOptionsBuilder =>
                {
                    var infrastructureAssembly = typeof(PMSDbContext).Assembly;
                    dbContextOptionsBuilder.UseSqlServer(sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly(infrastructureAssembly.FullName);
                        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "pms");
                    });
                });

            // 以具體型別註冊（不註冊 IMigrationService，避免覆蓋其他模組）；由 ModulePmsInitializer 自行解析
            services.AddScoped<PmsMigrationService>();
            return services;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("[PMS.Infrastructure] 資料庫與持久化服務註冊失敗", ex);
        }
    }
}
