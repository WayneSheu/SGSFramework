namespace SGSFramework.ApiInfrastructure.Extensions;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.ApiInfrastructure.Filters;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Filters;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Controllers.Providers;
using SGSFramework.Core.Converters; // 引用 NullableGuidJsonConverter 所在的命名空間
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.Services;

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

        // 註冊 Core 權限授權驗證服務 (DI)
        services.AddScoped<IPermissionAuthorizationService, PermissionAuthorizationService>();

        // 一次性註冊 Controller 並進行全域鏈式配置
        services.AddControllers(options =>
        {
            // 1. 註冊 MVC 全域 Authorization Filter
            options.Filters.Add<PermissionAuthorizationFilter>();
        })
            .ConfigureApplicationPartManager(manager =>
            {
                // 2. 注入內部控制器特性提供者 (支援模組內部 Controller 發現)
                manager.FeatureProviders.Add(new InternalControllerFeatureProvider());
            })
            .AddJsonOptions(options =>
            {
                // 3. 註冊客製化 Nullable Guid 轉譯器，自動將 API Payload 的空字串 "" 轉為 null
                options.JsonSerializerOptions.Converters.Add(new NullableGuidJsonConverter());
            });

        services.AddScoped<IControllerMetadataService, ControllerMetadataService>();
        return services;
    }
}