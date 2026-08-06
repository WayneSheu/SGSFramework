using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGS.Modules.ORG.Application.Features.Laboratories;
using SGS.Modules.ORG.Application.Services;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.ModulePlugin.Systems.Module;

namespace SGS.Modules.ORG.Extensions
{ 
    public static class ServiceCollectionExtensions
    {

        public static IServiceCollection AddModuleOrg(this IServiceCollection services, IConfiguration config)
        {


            // 註冊服務並記錄
            //services.AddScoped<IOrganizationService, OrganizationService>();

            // 註冊監控器 Metadata
            // 透過 BuildServiceProvider 取得監控服務實例 (或從參數傳入)
            //var monitor = services.BuildServiceProvider().GetRequiredService<ServiceRegistryMonitor>(); 
            //monitor.RegisterModuleInfo("OrgModule", $"MediatR registered from {assembly.FullName}");


            return services;
        }



    }
}
