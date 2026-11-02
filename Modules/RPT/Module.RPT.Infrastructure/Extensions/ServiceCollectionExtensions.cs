using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGS.Modules.RPT.Infrastructure.Dbcontexts;
using SGS.Modules.RPT.Infrastructure.Services;
using SGSFramework.AuditLog.Extensions;
using SGSFramework.Core.Migrations;

namespace SGS.Modules.RPT.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Infrastructure 層之 DbContext 與 Migration 服務
    /// </summary>
    public static IServiceCollection AddModuleRptInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            services.AddModuleDatabaseWithAudit<RPTDbContext>(
                configuration,
                "PersistentSettings:ConnectionStrings",
                "rpt",
                dbContextOptionsBuilder =>
                {
                    var infrastructureAssembly = typeof(RPTDbContext).Assembly;
                    dbContextOptionsBuilder.UseSqlServer(sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly(infrastructureAssembly.FullName);
                        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "rpt");
                    });
                });

            // 各模組皆以 IMigrationService 註冊；模組初始化器自行解析，以 RPT 專屬型別區分
            services.AddScoped<RptMigrationService>();
            return services;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("[RPT.Infrastructure] 資料庫與持久化服務註冊失敗", ex);
        }
    }
}
