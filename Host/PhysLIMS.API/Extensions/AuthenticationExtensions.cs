using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace PhysLIMS.API.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// 根據執行環境 (IIS 或 Kestrel) 安全配置身分驗證，避免在 IIS 下註冊過度的 NegotiateHandler。
    /// </summary>
    public static IServiceCollection AddCustomAuthentication(this IServiceCollection services, bool isIisHosted)
    {
        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultScheme = "Bearer";
        });

        if (isIisHosted)
        {
            // IIS 託管模式：驗證由 IIS 本機模組 (Kernel-mode) 完成，僅需設定 IISServerOptions 允許自動驗證
            services.Configure<IISServerOptions>(options =>
            {
                options.AutomaticAuthentication = true;
            });
        }
        else
        {
            // Kestrel 獨立執行模式：檢查避免重複註冊，並啟用 Kestrel 專屬的 NegotiateHandler
            var isNegotiateRegistered = services.Any(sd =>
                sd.ImplementationType == typeof(NegotiateHandler) ||
                sd.ServiceType == typeof(NegotiateHandler));

            if (!isNegotiateRegistered)
            {
                authBuilder.AddNegotiate();
            }
        }

        return services;
    }
}