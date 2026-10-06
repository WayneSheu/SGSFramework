namespace SGSFramework.ApiInfrastructure.DependencyInjection;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddSPAModulePlugin();

        return services;
    }
}