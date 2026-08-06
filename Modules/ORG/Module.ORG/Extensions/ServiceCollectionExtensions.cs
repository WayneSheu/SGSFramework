using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGS.Modules.ORG.Application;
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

            return services;
        }
    }
}
