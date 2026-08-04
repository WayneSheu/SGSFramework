using Microsoft.Extensions.DependencyInjection;
using SGSFramework.VerifyLedger.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.VerifyLedger.Extensions
{
    /// <summary>
    /// 總帳驗證服務的相依注入擴充方法
    /// </summary>
    public static class LedgerServiceCollectionExtensions
    {
        /// <summary>
        /// 註冊開放式泛型總帳驗證服務至 DI 容器，解決無法動態解析的問題
        /// </summary>
        public static IServiceCollection AddLedgerVerificationServices(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            // 關鍵修正：必須註冊開放式泛型 (Open Generic)，讓 Controller 執行 MakeGenericType 時能順利動態解析封閉式泛型執行個體
            services.AddScoped(typeof(ILedgerVerificationService<,>), typeof(MssqlLedgerVerificationService<,>));

            return services;
        }
    }
}
