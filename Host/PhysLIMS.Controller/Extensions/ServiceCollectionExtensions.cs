using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.ApiInfrastructure.Filters;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Controllers.Providers;
using SGSFramework.ModulePlugin.Systems.Controller.Repositories;

namespace SGSFramework.ApiInfrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// ModularMonolith 架構中，將 Controller 的註冊獨立出來，並使用 InternalControllerFeatureProvider 來支援內部控制器的發現與註冊。
    /// </summary>
    /// <param name="services">DI 服務容器</param>
    /// <param name="config">應用程式組態設定</param>
    /// <returns>IServiceCollection 實例</returns>
    public static IServiceCollection AddControllerInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        // 1. 初始化控制器註冊，注入內部控制器特性提供者
        services.AddControllers()
            .ConfigureApplicationPartManager(manager =>
            {
                manager.FeatureProviders.Add(new InternalControllerFeatureProvider());
            });

        // 註冊 Core 權限授權驗證服務 (DI)
        services.AddScoped<IPermissionAuthorizationService, PermissionAuthorizationService>();

        // 註冊 MVC 全域 Authorization Filter
        services.AddControllers(options =>
        {
            options.Filters.Add<PermissionAuthorizationFilter>();
        });

        return services;
    }
}