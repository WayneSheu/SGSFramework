using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.ModulePlugin.Systems.Module;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Application.Features.Laboratories.Query;
using SGS.Modules.ORG.Application.Services;

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


            // [修正] 移除危險的 services.BuildServiceProvider() 
            // 若一定要註冊監控 Metadata，請改用 TryAdd 或透過具備 DI 支援的方式處理，
            // 例如改用具名或直接由外部傳入實例，或者暫時註解掉這行測試看看。

            return services;
        }



    }
}
