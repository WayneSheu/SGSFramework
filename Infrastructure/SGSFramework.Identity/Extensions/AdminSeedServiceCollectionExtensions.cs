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
    using SGSFramework.Identity.Abstractions.Strategies;
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

            // 1. 綁定 SeedAdminOptions 區段
            services.Configure<SeedAdminOptions>(
                configuration.GetSection(SeedAdminOptions.SectionName));

            // 2. 補上：綁定 SystemRolePermissionSeedOptions 區段，解決 Roles 為空的問題
            services.Configure<SystemRolePermissionSeedOptions>(
                configuration.GetSection(SystemRolePermissionSeedOptions.SectionName));

            // 2.1 綁定 UserProvisioningOptions：支援物件格式 { "StrategyType": "Atomic" } 與舊版純字串格式 "Atomic"，
            //     並於啟動時驗證策略名稱可對應 UserProvisioningStrategyType 列舉
            var provisioningSection = configuration.GetSection(UserProvisioningOptions.SectionName);
            services.AddOptions<UserProvisioningOptions>()
                .Configure(options =>
                {
                    if (!string.IsNullOrWhiteSpace(provisioningSection.Value))
                    {
                        options.StrategyType = provisioningSection.Value;
                    }
                    else
                    {
                        provisioningSection.Bind(options);
                    }
                })
                .Validate(
                    options => Enum.TryParse<UserProvisioningStrategyType>(options.StrategyType, ignoreCase: true, out _),
                    $"{UserProvisioningOptions.SectionName}:StrategyType 必須為 {string.Join(" / ", Enum.GetNames<UserProvisioningStrategyType>())} 其中之一。")
                .ValidateOnStart();

            // 3. 帶入泛型 TDbContext 註冊 PermissionSeederService
            services.AddScoped<ISystemRolePermissionSeedService, SystemRolePermissionSeedService<TDbContext>>();

            // 4. 註冊 Seeder 服務
            services.AddScoped<IAdminSeederService, AdminSeederService>();

            // 5. 註冊 Startup HostedService 自動掛載
            services.AddHostedService<AdminSeedHostedService>();

            return services;
        }
    }
}