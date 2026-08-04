using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.Extensions.DependencyInjection;

namespace SGSFramework.Core.SSOs
{


    public static class ServiceCollectionExtensions
    {
        // 注入 SES.Core 的服務

        public static IServiceCollection AddSSOServices(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            //在本地容器注入 Windows 協商認證服務
            services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
                .AddNegotiate();

            return services;
        }



    }
}