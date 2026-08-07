using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.HostedServices;
using SGSFramework.Identity.Options;
using SGSFramework.Identity.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.Extensions
{
    public static class AdminSeedServiceCollectionExtensions
    {
        public static IServiceCollection AddProductionAdminSeeder(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            // 綁定 Configuration 區段
            services.Configure<SeedAdminOptions>(
                configuration.GetSection(SeedAdminOptions.SectionName));

            // 註冊 Seeder 服務
            services.AddScoped<IAdminSeederService, AdminSeederService>();

            // 註冊 Startup HostedService 自動掛載
            services.AddHostedService<AdminSeedHostedService>();

            return services;
        }
    }
}
