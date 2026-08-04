using Microsoft.Extensions.DependencyInjection;

namespace SGSFramework.Core.ApiDoc.Extensions
{


    public static class ServiceCollectionExtensions
    {
        // 注入 SES.Core 的服務

        public static IServiceCollection AddAPIDocServices(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);
            
            // 註冊服務以落實依賴注入
            services.AddScoped<IApiDocService, ApiDocService>();
            return services;
        }



    }
}