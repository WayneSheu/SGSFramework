using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGS.Modules.MAN.Infrastructure.Dbcontexts;
using SGS.Modules.MAN.Infrastructure.Services;
using SGSFramework.AuditLog.Extensions;

namespace SGS.Modules.MAN.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Infrastructure 層：MANDbContext（schema man，含資料異動稽核）與 Migration 服務
    /// </summary>
    public static IServiceCollection AddModuleManInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            services.AddModuleDatabaseWithAudit<MANDbContext>(
                configuration,
                "PersistentSettings:ConnectionStrings",
                "man",
                dbContextOptionsBuilder =>
                {
                    var infrastructureAssembly = typeof(MANDbContext).Assembly;
                    dbContextOptionsBuilder.UseSqlServer(sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly(infrastructureAssembly.FullName);
                        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "man");
                    });
                });

            // 以具體型別註冊（不註冊 IMigrationService，避免覆蓋其他模組）；由 ModuleManInitializer 自行解析
            services.AddScoped<ManMigrationService>();
            return services;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("[MAN.Infrastructure] 資料庫與持久化服務註冊失敗", ex);
        }
    }
}
