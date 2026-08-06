using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Application.Features.Laboratories.Query;
using SGS.Modules.ORG.Application.Services;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.ModulePlugin.Systems.Module;

namespace SGS.Modules.ORG.Application.Extensions
{ 
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddModuleOrgApplication(this IServiceCollection services, IConfiguration config)
        {
            // 1. 註冊一般服務
            services.AddScoped<IOrganizationService, OrganizationService>();

            var asm = typeof(GetLaboratoriesHandler).Assembly;

            // 2. 註冊 MediatR 核心服務（提供 IMediator 實例）
            services.AddMediatR(cfg => {
                cfg.RegisterServicesFromAssemblies(asm);
            });

            // 3. 防禦性手動註冊：確保特定 Handler 100% 寫入 DI 容器
            services.AddTransient<
                IRequestHandler<GetLaboratoriesQuery, List<LaboratoryDto>>,
                GetLaboratoriesHandler>();

            return services;
        }



    }
}
