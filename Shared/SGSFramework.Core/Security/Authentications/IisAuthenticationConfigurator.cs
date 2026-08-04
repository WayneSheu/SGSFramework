using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Security.Authentications
{
    /// <summary>
    /// IIS驗證配置器
    /// </summary>
    public class IisAuthenticationConfigurator : IAuthenticationConfigurator
    {
        private readonly ILogger<IisAuthenticationConfigurator> _logger;

        public IisAuthenticationConfigurator(ILogger<IisAuthenticationConfigurator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void ConfigureAuthentication(IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            try
            {
                // IIS 會原生處理 Windows Authentication，僅需宣告認證 Scheme，切勿加入 AddNegotiate()
                services.AddAuthentication(IISDefaults.AuthenticationScheme);
                _logger.LogInformation("[Auth] Successfully configured Native IIS Windows Authentication scheme.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Auth] Failed to configure IIS Authentication scheme.");
                throw;
            }
        }
    }
}
