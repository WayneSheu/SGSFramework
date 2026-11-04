using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using SGS.Modules.FMS.Infrastructure.Dbcontexts;
using SGS.Modules.FMS.Infrastructure.Options;
using SGS.Modules.FMS.Infrastructure.Services;
using SGS.Modules.FMS.Infrastructure.Storage;
using SGSFramework.AuditLog.Extensions;
using SGSFramework.Core.Configurations;

namespace SGS.Modules.FMS.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 註冊 Infrastructure 層：FMSDbContext（schema fms）、Migration 服務、檔案管理組態（啟動時驗證）與儲存體工廠
    /// </summary>
    public static IServiceCollection AddModuleFmsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            services.AddModuleDatabaseWithAudit<FMSDbContext>(
                configuration,
                "PersistentSettings:ConnectionStrings",
                "fms",
                dbContextOptionsBuilder =>
                {
                    var infrastructureAssembly = typeof(FMSDbContext).Assembly;
                    dbContextOptionsBuilder.UseSqlServer(sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly(infrastructureAssembly.FullName);
                        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "fms");
                    });
                });

            // 以具體型別註冊（不註冊 IMigrationService，避免覆蓋其他模組）；由 ModuleFmsInitializer 自行解析
            services.AddScoped<FmsMigrationService>();

            // 檔案管理組態沿用 Host 的 FileManagement 區段；Enabled = false 時不驗證儲存組態
            services.AddValidatedOptions<FileManagementOptions>(FileManagementOptions.SectionName);
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<FileManagementOptions>, FileManagementOptionsValidator>());
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddSingleton<IStorageFactory, StorageFactory>();
            return services;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("[FMS.Infrastructure] 資料庫與持久化服務註冊失敗", ex);
        }
    }
}
