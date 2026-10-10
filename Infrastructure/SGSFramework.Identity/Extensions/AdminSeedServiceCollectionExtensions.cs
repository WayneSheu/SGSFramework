// ==========================================
// 檔案路徑: src/SGSFramework.Identity/Extensions/AdminSeedServiceCollectionExtensions.cs
// 架構層級: Identity / Extensions
// ==========================================

#nullable enable

namespace SGSFramework.Identity.Extensions
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using SGSFramework.Core.Abstractions.DbContexts;
    using SGSFramework.Identity.Abstractions;
    using SGSFramework.Identity.HostedServices;
    using SGSFramework.Identity.Options;
    using SGSFramework.Identity.Services;
    using System;

    public static class AdminSeedServiceCollectionExtensions
    {
        public static IServiceCollection AddProductionAdminSeeder<TDbContext>(
            this IServiceCollection services,
            IConfiguration configuration)
            where TDbContext : DbContext, ITokenDbContext
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configuration);

            // 綁定 Configuration 區段
            services.Configure<SeedAdminOptions>(
                configuration.GetSection(SeedAdminOptions.SectionName));

            // 修正：帶入泛型 TDbContext 註冊 PermissionSeederService
            services.AddScoped<ISystemRolePermissionSeedService, SystemRolePermissionSeedService<TDbContext>>();

            // 註冊 Seeder 服務
            services.AddScoped<IAdminSeederService, AdminSeederService>();

            // 註冊 Startup HostedService 自動掛載
            services.AddHostedService<AdminSeedHostedService>();

            return services;
        }
    }
}