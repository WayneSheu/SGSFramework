using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SGSFramework.Alert.Abstractions;
using SGSFramework.Alert.Channels;
using SGSFramework.Alert.Floods;
using SGSFramework.Alert.Services;
using SGSFramework.Alert.Workers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;

namespace SGSFramework.Alert.Extensions
{
    public static class AlertServiceExtensions
    {
        /// <summary>
        /// 註冊企業級告警基礎設施與策略管道
        /// </summary>
        public static IServiceCollection AddEnterpriseAlertInfrastructure(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);


            services.AddMemoryCache();
            services.TryAddSingleton<IAlertFloodSuppressor, InMemoryAlertFloodSuppressor>();
            // 1. 建立高效能 In-Memory 告警佇列
            var alertChannel = Channel.CreateUnbounded<AlertMessageModel>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            services.AddSingleton(alertChannel.Reader);
            services.AddSingleton(alertChannel.Writer);

            // 2. 註冊多重告警管道策略 (Strategy Pattern)
            services.AddSingleton<IAlertNotificationChannel, SmtpNotificationChannel>();
            services.AddSingleton<IAlertNotificationChannel, LoggingFallbackNotificationChannel>();

            // 3. 註冊應用層調度服務與背景 Worker
            services.AddSingleton<IAlertDispatcherService, AlertDispatcherService>();
            services.AddHostedService<AlertWorkerService>();

            return services;
        }
    }
}
