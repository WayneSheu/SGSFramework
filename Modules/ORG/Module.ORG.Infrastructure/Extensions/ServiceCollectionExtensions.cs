using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.Persistent.Extensions;
using SGSFramework.Persistent.Repositories.Hierarchy;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGS.Modules.ORG.Infrastructure.Entities.Org;

namespace SGS.Modules.ORG.Infrastructure.Extensions
{


    public static class ServiceCollectionExtensions
    {
        // 
        // 注入模組的服務
        public static IServiceCollection AddModuleORGInfrastructure(this IServiceCollection services)
        {

            //注冊Repository
            services.AddScoped<IHierarchicalRepository<ORGDbContext, Organization>,
             HierarchicalRepository<ORGDbContext, Organization>>();

            return services;
        }

    }
}