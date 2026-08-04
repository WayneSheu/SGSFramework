using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SGSFramework.DataProtection;
using SGSFramework.DataProtection.Abstractions;
using SGSFramework.DataProtection.Clients;
using SGSFramework.DataProtection.Configurations;
using SGSFramework.DataProtection.Gateways;
using SGSFramework.DataProtection.Handlers;
using SGSFramework.DataProtection.Strategies;
using System.Linq.Expressions;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SGSFramework.DataProtection.Extensions
{


    /// <summary>
    /// 整合完成了從 配置 -> 註冊 -> 攔截器注入 -> 服務介面化 的完整生產級流程。
    /// Program.cs 將維持極簡風格：
    /// builder.Services.AddEncryptionService(builder.Configuration, serviceProvider);
    /// </summary>
    public static class DataProtectionExtensions
    { 
        public static IServiceCollection AddDataProtectionService(this IServiceCollection services,IConfiguration configuration)
        {

            //var logger = serviceProvider.GetRequiredService<ILogger<DataProtectionExtensions>>();

            // 統一註冊所有策略
            services.AddScoped<IEncryptionStrategy, AesGcmEncryptionStrategy>();
            services.AddScoped<IEncryptionStrategy, RsaOaepEncryptionStrategy>();

            // 針對作業系統平台進行條件式篩選
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // 僅在 Windows 環境下註冊 DPAPI 策略
                services.AddScoped<IEncryptionStrategy, DpapiEncryptionStrategy>();
            }
            else
            {
                // 若非 Windows 環境，可選填記錄警告，避免混淆開發者
                //var logger = services.BuildServiceProvider().GetRequiredService<ILogger<Progrm>>();
                //logger.LogWarning("偵測到非 Windows 環境，系統將自動停用 Windows-DPAPI 加密策略。");
            }

            services.AddEncryption(configuration)
                    .AddHmsClient(configuration)
                    .AddDiApi();


            return services;
        }


        public static IServiceCollection AddDiApi(this IServiceCollection services)
        {
            services.AddScoped<IDiApi, DiApi>();
            return services;
        }

        public static IServiceCollection AddEncryption(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. 綁定配置區段
            //services.Configure<EncryptionOptions>(configuration.GetSection(EncryptionOptions.SectionName));
            // 若 GetSection 報錯，請先確認 configuration 不為 null
            var section = configuration.GetSection(EncryptionOptions.SectionName);

            services.Configure<EncryptionOptions>(section);


            // 2. 註冊策略 (使用策略模式)
            services.AddTransient<IEncryptionStrategy, AesGcmEncryptionStrategy>();
            services.AddTransient<IEncryptionStrategy, RsaOaepEncryptionStrategy>();

            // 3. 註冊上下文容器
            services.AddSingleton<EncryptionContext>();

            return services;
        }

        public static IServiceCollection AddHmsClient(this IServiceCollection services, IConfiguration userConfiguration)
        {
            // 1. 定義套件預設配置
            var defaultSettings = new Dictionary<string, string?>
            {
                {"HmsSettings:BaseUrl", "https://hms-default.internal"},
                {"HmsSettings:SharedSecret", "Default-Fallback-Secret-DO-NOT-USE-IN-PROD"}
            };

            // 2. 建立分層配置：預設值 -> 主專案設定 (主專案設定會自動覆蓋預設值)
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(defaultSettings) // 最底層：預設值
                .AddConfiguration(userConfiguration)    // 最上層：外部注入的主配置 (優先級最高)
                .Build();

            // 3. 綁定配置到 Options 模型
            services.Configure<HmsOptions>(configuration.GetSection(HmsOptions.SectionName));

            // 4. 註冊 Handler 與 HttpClient
            //註冊簽章攔截器 (Transient)
            services.AddTransient<HmsSignatureHandler>();
            //註冊 HttpClient 與攔截器 (HttpMessageHandler)
            services.AddHttpClient<IHmsClient, HmsClient>(client =>
            {
                var options = configuration.GetSection(HmsOptions.SectionName).Get<HmsOptions>();
                client.BaseAddress = new Uri(options?.BaseUrl ?? "https://localhost");
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            // 自動掛載簽章邏輯
            .AddHttpMessageHandler<HmsSignatureHandler>();

            return services;
        }

    }
}
