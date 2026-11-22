using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGS.Modules.MTG.Infrastructure.Dbcontexts;
using SGS.Modules.MTG.Infrastructure.Services;
using SGSFramework.AuditLog.Extensions;

namespace SGS.Modules.MTG.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Infrastructure 層：MTGDbContext（schema mtg，含資料異動稽核）與 Migration 服務
    /// </summary>
    public static IServiceCollection AddModuleMtgInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            services.AddModuleDatabaseWithAudit<MTGDbContext>(
                configuration,
                "PersistentSettings:ConnectionStrings",
                "mtg",
                dbContextOptionsBuilder =>
                {
                    var infrastructureAssembly = typeof(MTGDbContext).Assembly;
                    dbContextOptionsBuilder.UseSqlServer(sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly(infrastructureAssembly.FullName);
                        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "mtg");
                    });
                });

            // 以具體型別註冊（不註冊 IMigrationService，避免覆蓋其他模組）；由 ModuleMtgInitializer 自行解析
            services.AddScoped<MtgMigrationService>();
            return services;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("[MTG.Infrastructure] 資料庫與持久化服務註冊失敗", ex);
        }
    }
}
