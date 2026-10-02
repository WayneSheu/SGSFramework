// Path: Infrastructure/SGSFramework.SystemLog/Extensions/SerilogExtensions.cs
#nullable enable
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using SGSFramework.Core.Abstractions.Logings;
using SGSFramework.Core.Abstractions.Notifications;
using SGSFramework.Core.Abstractions.Processors;
using SGSFramework.Core.Mask;
using SGSFramework.SystemLog.BackgroundServices;
using SGSFramework.SystemLog.Channels;
using SGSFramework.SystemLog.Notifications;
using SGSFramework.SystemLog.Options;
using SGSFramework.SystemLog.Services;
using SGSFramework.SystemLog.Sinks;

namespace SGSFramework.SystemLog.Extensions
{
    /// <summary>
    /// 提供 Serilog 的核心配置與雙管道非阻塞整合之基礎設施擴充方法
    /// </summary>
    public static class SerilogExtensions
    {
        private static readonly SystemLogChannel _systemChannel = new(capacity: 2000);
        private static readonly SecurityLogChannel _securityChannel = new(capacity: 1000);
        private static readonly AlertMemoryChannel _alertChannel = new();

        /// <summary>
        /// 將 Serilog 配置為 ASP.NET Core 的日誌提供程序，並自動在內部完整註冊管道、處理器、組態繫結與背景 Worker。
        /// </summary>
        public static WebApplicationBuilder AddSystemLog(this WebApplicationBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // =========================================================================
            // 1. 初始化動態日誌層級切換器 (LoggingLevelSwitch) 並註冊為 Singleton
            // =========================================================================
            var levelSwitch = new LoggingLevelSwitch(LogEventLevel.Information);
            builder.Services.AddSingleton(levelSwitch);

            // 執行基礎引導日誌 (確保在 Config 載入前有錯誤追蹤)
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .WriteTo.Console()
                .CreateBootstrapLogger();

            // 開啟 Serilog 內部除錯診斷
            Serilog.Debugging.SelfLog.Enable(msg => Console.WriteLine($"🔥 Serilog Internal Error: {msg}"));

            // =========================================================================
            // 2. 將 appsettings.json 的 AlertSettings 區段進行強型別繫結與啟動驗證
            // =========================================================================
            builder.Services.AddOptions<AlertSettings>()
                .Bind(builder.Configuration.GetSection("AlertSettings"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // =========================================================================
            // 3. 核心記憶體管道註冊 (全域單例)
            // =========================================================================
            builder.Services.AddSingleton(_systemChannel);
            builder.Services.AddSingleton(_securityChannel);
            builder.Services.AddSingleton(_alertChannel);

            // =========================================================================
            // 4. 告警通知與日誌查詢服務註冊
            // =========================================================================
            builder.Services.AddSingleton<IAlertCoordinator, AlertCoordinator>();
            builder.Services.AddSingleton<INotificationStrategy, EmailNotificationStrategy>();
            builder.Services.AddScoped<ISystemLogQueryService, SystemLogQueryService>();

            // =========================================================================
            // 5. 持久化核心處理器註冊 (Processors)
            // =========================================================================
            builder.Services.AddSingleton<SqlServerLogProcessor>();
            builder.Services.AddSingleton<ISecurityLogger, SecurityLogger>();
            builder.Services.AddSingleton<SqlServerSecurityProcessor>();

            // =========================================================================
            // 6. 常駐背景服務註冊 (Hosted Services / Workers 消費端)
            // =========================================================================
            builder.Services.AddHostedService<SystemLogPersistentWorker>();
            builder.Services.AddHostedService<SecurityLogPersistentWorker>();
            builder.Services.AddHostedService<AlertWorkerService>();

            // =========================================================================
            // 7. 配置主機關閉寬限時間
            // =========================================================================
            builder.Services.Configure<HostOptions>(options =>
            {
                options.ShutdownTimeout = TimeSpan.FromSeconds(15);
            });

            // =========================================================================
            // 8. 建立並繫結 Serilog 核心管線
            // =========================================================================
            builder.Host.UseSerilog((context, servicesProvider, loggerConfig) =>
            {
                try
                {
                    loggerConfig
                        .MinimumLevel.ControlledBy(levelSwitch) // 動態層級控管
                        .ReadFrom.Configuration(context.Configuration) // 讀取 appsettings.json 配置
                        .ReadFrom.Services(servicesProvider); // 讓 Serilog 可存取 DI 服務

                    loggerConfig.ConfigurationSerilogMasking();

                    // 管道分流 (1)：一般系統日誌路由        
                    loggerConfig.WriteTo.Logger(lc => lc
                        .Filter.ByExcluding("LogType is not null or SourceContext = 'SecurityEventSource'")
                        .WriteTo.Sink(new PersistentChannelSink(_systemChannel)));

                    // 管道分流 (2)：安全日誌路由
                    loggerConfig.WriteTo.Logger(lc => lc
                        .Filter.ByIncludingOnly("LogType is not null or SourceContext = 'SecurityEventSource'")
                        .WriteTo.Sink(new PersistentChannelSink(_securityChannel)));

                    // 動態事件即時告警流過濾與分流綁定
                    loggerConfig.WriteTo.Conditional(
                        evt =>
                        {
                            var isSecurity = evt.Properties.TryGetValue("LogType", out var logType) &&
                                             logType is ScalarValue sv &&
                                             "Security".Equals(sv.Value?.ToString(), StringComparison.OrdinalIgnoreCase);

                            return isSecurity ? evt.Level >= LogEventLevel.Warning : evt.Level >= LogEventLevel.Error;
                        },
                        wt => wt.AlertingSink(_alertChannel.Writer)
                    );
                }
                catch (Exception ex)
                {
                    Serilog.Debugging.SelfLog.WriteLine($"[Serilog Root Configuration Fatal] Failed to combine masking pipeline and channels: {ex.Message}");
                    throw;
                }
            });

            return builder;
        }
    }
}