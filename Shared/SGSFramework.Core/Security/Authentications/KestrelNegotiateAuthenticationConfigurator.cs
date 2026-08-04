using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Security.Authentications
{
    /// <summary>
    /// 用於在 Kestrel 環境中設定身份驗證。
    /// </summary>
    public class KestrelNegotiateAuthenticationConfigurator : IAuthenticationConfigurator
    {
        private readonly ILogger<KestrelNegotiateAuthenticationConfigurator> _logger;

        public KestrelNegotiateAuthenticationConfigurator(ILogger<KestrelNegotiateAuthenticationConfigurator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void ConfigureAuthentication(IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            try
            {
                // 僅在 Kestrel 環境下啟用純 C# 的 Negotiate 套件
                services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
                        .AddNegotiate();
                _logger.LogInformation("[Auth] Successfully configured Kestrel Negotiate Authentication.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Auth] Failed to configure Kestrel Negotiate Authentication.");
                throw;
            }
        }
    }
}
