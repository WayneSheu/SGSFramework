namespace SGSFramework.ApiInfrastructure.DependencyInjection;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGSFramework.ApiInfrastructure.Services.DatabaseSchemas;
using SGSFramework.ApiInfrastructure.Filters;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Filters;
using SGSFramework.AuthTokenBucket.Queries.Menuitems;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Controllers.Providers;
using SGSFramework.Core.Converters; // 引用 NullableGuidJsonConverter 所在的命名空間
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.Services;
using SGSFramework.SPAModulePlugin.Extensions;
using SGSFramework.SPAModulePlugin.Presentation.Controllers.v1;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// ModularMonolith 架構中，將 Controller 的註冊獨立出來，
    /// 整合內部控制器發現、全域 Authorization Filter 與全域 JSON 轉譯器。
    /// </summary>
    /// <param name="services">DI 服務容器</param>
    /// <param name="config">應用程式組態設定</param>
    /// <returns>IServiceCollection 實例</returns>
    public static IServiceCollection AddControllerInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.AddScoped<IPermissionAuthorizationService, PermissionAuthorizationService>();

        var mvcBuilder = services.AddControllers(options =>
        {
            // 先擋下已手動停用的功能，再進行權限比對
            options.Filters.Add<FunctionStatusAuthorizationFilter>();
            options.Filters.Add<PermissionAuthorizationFilter>();
        })
        .ConfigureApplicationPartManager(manager =>
        {
            manager.FeatureProviders.Add(new InternalControllerFeatureProvider());
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new NullableGuidJsonConverter());
        });

        // 確保 SPAModuleController 所在的 Assembly 被 MVC 框架發現
        mvcBuilder.AddApplicationPart(typeof(SPAModuleController).Assembly);

        services.AddScoped<IControllerMetadataService, ControllerMetadataService>();

        // 系統配置管理：設定服務 + Identity 規則執行期套用 (啟動時自資料庫載入)
        services.AddScoped<ISystemSettingsService, SystemSettingsService>();
        services.AddSingleton<IdentityPolicyRuntime>();
        services.AddHostedService<IdentityPolicyStartupLoader>();
        services.TryAddSingleton(TimeProvider.System);

        // 系統帳號（M2M）：管理 / 權杖換發服務、JwtBearer 即時撤銷檢查、[RequiresSystemScope] 目錄
        services.AddScoped<ISystemAccountService, SystemAccountService>();
        services.AddScoped<ISystemAccountTokenValidator, SystemAccountTokenValidator>();
        services.AddSingleton<SystemScopeCatalog>();
        services.AddScoped<IEntityDescriptionProvider, EfModelDescriptionProvider>();
        services.AddScoped<IMigrationHistoryProvider, EfMigrationHistoryProvider>();
        services.AddScoped<IDatabaseSchemaService, SqlServerDatabaseSchemaService>();
        services.AddSPAModulePlugin();

        return services;
    }
}