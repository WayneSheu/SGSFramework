using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.CodeSecurity.Abstractions;
using SGSFramework.CodeSecurity.Options;
using SGSFramework.CodeSecurity.Services.Signings;
using SGSFramework.CodeSecurity.Services.Verifications;
using SGSFramework.CodeSecurity.Strategies;

namespace SGSFramework.CodeSecurity.Extensions
{
    public static class CodeSecurityExtensions
    {
        public static IServiceCollection AddCodeSecurity(this IServiceCollection services, IConfiguration configuration)
        {
            // 綁定組態
            services.AddOptions<CodeSecurityOptions>()
                .Bind(configuration.GetSection("CodeSecurity"))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // 根據組態或環境變數動態注入簽署策略
            // 優先讀取組態檔 "Security:SigningMode"，若無則讀取環境變數
            services.AddSingleton<ISigningStrategy>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var mode = config["Security:SigningMode"] ?? Environment.GetEnvironmentVariable("SIGNING_MODE");

                return mode?.ToLowerInvariant() switch
                {
                    "tpm" => new LocalTpmSigningStrategy(),
                    "offline" => new OfflineFileSigningStrategy(),
                    _ => throw new NotSupportedException($"Signing strategy '{mode}' is not supported.")
                };
            });

            // 註冊簽署處理服務
            services.AddSingleton<ISigningService, ModularSigningService>();

            // 註冊應用層 Context，確保服務層可直接使用 ISigningStrategy 邏輯
            services.AddSingleton<ISecurityService, WindowsSignatureService>();

            return services;
        }
    }
}