using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Security.Authentications
{
    /// <summary>
    /// 驗證配置器接口，用於配置驗證服務。
    /// </summary>
    public interface IAuthenticationConfigurator
    {
        void ConfigureAuthentication(IServiceCollection services);
    }
}
