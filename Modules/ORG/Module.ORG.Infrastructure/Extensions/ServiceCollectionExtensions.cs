using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGS.Modules.ORG.Infrastructure.Services;
using SGSFramework.AuditLog.Extensions;
using SGSFramework.Core.Migrations;
using SGSFramework.Persistent.Repositories.Hierarchy;

namespace SGS.Modules.ORG.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Infrastructure 層之 DbContext、Repositories 與 Migration 服務
    /// </summary>
    public static IServiceCollection AddModuleOrgInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            // 1. 註冊階層式 Hierarchy Repository
            //services.TryAddScoped(
            //    typeof(IHierarchicalRepository<ORGDbContext, Organization>),
            //    typeof(HierarchicalRepository<ORGDbContext, Organization>));

            // 將 TryAddScoped 改為明確的 AddScoped，確保泛型倉儲介面強制寫入 DI 容器
            //services.AddScoped(
            //    typeof(IHierarchicalRepository<ORGDbContext, Organization>),
            //    typeof(HierarchicalRepository<ORGDbContext, Organization>));

            // [修正] 必須全面採用開放泛型（Open Generics）註冊階層式倉儲，使 DI 容器能夠在執行期依據不同實體動態解析對應的 IHierarchicalRepository
            services.AddScoped(typeof(IHierarchicalRepository<,>), typeof(HierarchicalRepository<,>));


            // 2. 配置持久化資料庫與 Schema 自動化
            services.AddModuleDatabaseWithAudit<ORGDbContext>(
                configuration,
                "PersistentSettings:ConnectionStrings",
                "org",
                dbContextOptionsBuilder =>
                {
                    var infrastructureAssembly = typeof(ORGDbContext).Assembly;
                    dbContextOptionsBuilder.UseSqlServer(sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly(infrastructureAssembly.FullName);
                        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "org");
                    });
                });

            // 3. 註冊資料庫遷移服務
            services.AddScoped<IMigrationService, OrgMigrationService>();

            return services;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("[ORG.Infrastructure] 資料庫與持久化服務註冊失敗", ex);
        }
    }
}