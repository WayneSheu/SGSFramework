using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGS.Modules.RUL.Infrastructure.Dbcontexts;
using SGS.Modules.RUL.Infrastructure.Services;
using SGSFramework.AuditLog.Extensions;

namespace SGS.Modules.RUL.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Infrastructure 層：RULDbContext（schema rul，含資料異動稽核）與 Migration 服務
    /// </summary>
    public static IServiceCollection AddModuleRulInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            services.AddModuleDatabaseWithAudit<RULDbContext>(
                configuration,
                "PersistentSettings:ConnectionStrings",
                "rul",
                dbContextOptionsBuilder =>
                {
                    var infrastructureAssembly = typeof(RULDbContext).Assembly;
                    dbContextOptionsBuilder.UseSqlServer(sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly(infrastructureAssembly.FullName);
                        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "rul");
                    });
                });

            // 以具體型別註冊（不註冊 IMigrationService，避免覆蓋其他模組）；由 ModuleRulInitializer 自行解析
            services.AddScoped<RulMigrationService>();
            return services;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("[RUL.Infrastructure] 資料庫與持久化服務註冊失敗", ex);
        }
    }
}
