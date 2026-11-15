using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGS.Modules.CAL.Infrastructure.Dbcontexts;
using SGS.Modules.CAL.Infrastructure.Services;
using SGSFramework.AuditLog.Extensions;

namespace SGS.Modules.CAL.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Infrastructure 層：CALDbContext（schema cal，含資料異動稽核）與 Migration 服務
    /// </summary>
    public static IServiceCollection AddModuleCalInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            services.AddModuleDatabaseWithAudit<CALDbContext>(
                configuration,
                "PersistentSettings:ConnectionStrings",
                "cal",
                dbContextOptionsBuilder =>
                {
                    var infrastructureAssembly = typeof(CALDbContext).Assembly;
                    dbContextOptionsBuilder.UseSqlServer(sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly(infrastructureAssembly.FullName);
                        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "cal");
                    });
                });

            // 以具體型別註冊（不註冊 IMigrationService，避免覆蓋其他模組）；由 ModuleCalInitializer 自行解析
            services.AddScoped<CalMigrationService>();
            return services;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("[CAL.Infrastructure] 資料庫與持久化服務註冊失敗", ex);
        }
    }
}
