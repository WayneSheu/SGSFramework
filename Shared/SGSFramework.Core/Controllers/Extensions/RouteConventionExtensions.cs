using Microsoft.Extensions.DependencyInjection;
using SGSFramework.Core.Controllers.Conventions;

namespace SGSFramework.Core.Controllers.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    public static class RouteConventionExtensions
    {
        //
        public static IServiceCollection AddRouteConvention(this IServiceCollection services, string apiVersionPrefix = "v1")
        {
            services.AddControllers(options =>
            {
                // 全域套用版本前綴慣例
                options.Conventions.Add(new ApiVersionRouteConvention(apiVersionPrefix));
            });

            return services;

        }
    }
}
