using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace SGSFramework.Core.Filters.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    public static class FiltersExtensions
    {
        // 1. 註冊 Filter 到 DI 容器
        public static void AddControllersWithFilter(this WebApplicationBuilder builder)
        {

            // 註冊 Filter
            builder.Services.AddControllers(options =>
            {
                options.Filters.Add<LogContextFilter>();
            });
        }
    }
}
