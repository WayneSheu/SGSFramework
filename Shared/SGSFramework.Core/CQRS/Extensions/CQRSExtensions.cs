using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.Core.CQRS.Behaviors;
using SGSFramework.Core.CQRS.Extensions;
using System.Reflection;

namespace SGSFramework.Core.CQRS.Extensions
{
    /// <summary>
    /// 
    /// </summary>
    public static class CQRSExtensions
    {
        //

        public static void AddMediatR(this WebApplicationBuilder builder)
        {


            builder.Services.AddMediatR(options =>
            {
                options.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());// 註冊 Shared 專案 (SES.Core) 的 Pipeline Behaviors (如 Logging/Validation)

                options.AddOpenBehavior(typeof(LoggingBehavior<,>));// 紀錄Request與Response的行為
                options.AddOpenBehavior(typeof(ValidationBehaviour<,>));// 驗證Request的行為
                //options.AddOpenBehavior(typeof(PerformanceBehaviour<,>));// 紀錄Request執行時間的行為 ,LoggingBehavior 已經包含了紀錄執行時間的功能，所以這個行為可以省略
                options.AddOpenBehavior(typeof(ExceptionHandlingBehavior<,>));// 處理例外的行為

            });


        }


        public static IServiceCollection AddSESCQRS(this IServiceCollection services, params Assembly[] applicationAssemblies)
        {
            services.AddMediatR(cfg =>
            {
                // 1. 註冊 Shared 專案本身的 Assembly (用於尋找內建的 Pipeline Behaviors)
                cfg.RegisterServicesFromAssembly(typeof(CQRSExtensions).Assembly);

                // 2. 自動循環註冊從外部傳入（如 Application 專案）的所有 Assemblies
                foreach (var assembly in applicationAssemblies)
                {
                    cfg.RegisterServicesFromAssembly(assembly);
                }

                // 3. 註冊全域的 Pipeline Behaviors (如你 Log 中看到的 Logging/Validation)
                cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
                cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
                cfg.AddOpenBehavior(typeof(ExceptionHandlingBehavior<,>));
            });

            return services;
        }
    }
}
