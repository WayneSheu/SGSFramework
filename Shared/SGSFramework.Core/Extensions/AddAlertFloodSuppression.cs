using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGSFramework.Core.Abstractions.Alerts;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Extensions
{
    public static class AlertServiceCollectionExtensions
    {
        /// <summary>
        /// 註冊防洪抑制器與相關告警基礎設施服務
        /// </summary>
        public static IServiceCollection AddAlertFloodSuppression(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddMemoryCache();
            services.TryAddSingleton<IAlertFloodSuppressor, InMemoryAlertFloodSuppressor>();

            return services;
        }
    }
}
