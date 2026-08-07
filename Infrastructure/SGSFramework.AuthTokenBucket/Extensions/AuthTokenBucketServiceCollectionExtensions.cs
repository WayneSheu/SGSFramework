namespace SGSFramework.AuthTokenBucket.Extensions
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using SGSFramework.AuthTokenBucket.Abstractions;
    using SGSFramework.AuthTokenBucket.Configurations;
    using SGSFramework.AuthTokenBucket.Repositories;
    using SGSFramework.AuthTokenBucket.Servers;
    using SGSFramework.AuthTokenBucket.Services;
    using SGSFramework.Core.Abstractions.DbContexts;
    using SGSFramework.Core.Abstractions.Permissions;
    using System;
    using System.Linq;
    using System.Reflection;

    public static class AuthTokenBucketServiceCollectionExtensions
    {
        public static IServiceCollection AddTokenBucketAuthentication<TDbContext, TUser>(
            this IServiceCollection services,
            Action<AuthTokenBucketOptions> configureOptions,
            params Assembly[] assembliesToScan)
            where TDbContext : DbContext, ITokenDbContext
            where TUser : IdentityUser, new()
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);

            services.Configure(configureOptions);
            services.AddScoped<TokenManager>();
            services.AddScoped<TokenBucketEngine<TUser>>();
            services.AddScoped<ITokenStorageProvider, SqlTokenStorageProvider<TDbContext>>();
            services.AddScoped<IUserRefreshTokenRepository, UserRefreshTokenRepository<TDbContext>>();
            services.AddScoped<IUserRuntimeScopeService, UserRuntimeScopeService>();

            // 註冊 IPermissionManagementService 的泛型實作，讓 PermissionController 能順利注入
            services.AddScoped<IPermissionManagementService, PermissionManagementService<TDbContext>>();

            // 1. 自動彙整全域 AppDomain 中的所有 SGSFramework、SGS 模組與 PhysLIMS 主專案 Assemblies
            var entryAssembly = Assembly.GetEntryAssembly();
            var fullAssembliesToScan = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.FullName != null &&
                           (a.FullName.StartsWith("SGSFramework") ||
                            a.FullName.StartsWith("SGS.") ||
                            a.FullName.StartsWith("PhysLIMS") ||
                            (entryAssembly != null && a == entryAssembly)))
                .Union(assembliesToScan ?? Array.Empty<Assembly>())
                .Distinct()
                .ToArray();

            // 2. 實作 DynamicPermissionRegistry 預先進行全組件掃描與 BitMask 分配
            var registry = new DynamicPermissionRegistry(fullAssembliesToScan);

            // 註冊單例 IPermissionRegistry
            services.AddSingleton<IPermissionRegistry>(registry);

            // 3. 註冊 IPermissionSeedService 並注入完整 Assembly 清單，確保 DB 同步時不遺漏 SGSFramework.System
            services.AddScoped<IPermissionSeedService>(sp =>
                new PermissionSeedService<TDbContext>(
                    sp.GetRequiredService<TDbContext>(),
                    sp.GetRequiredService<IPermissionRegistry>(),
                    fullAssembliesToScan,
                    sp.GetRequiredService<ILogger<PermissionSeedService<TDbContext>>>()
                ));

            return services;
        }
    }

    public static class ApplicationBuilderExtensions
    {
        public static async Task<IApplicationBuilder> UsePermissionSeederAsync(this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var seedService = scope.ServiceProvider.GetService<IPermissionSeedService>();
            if (seedService != null)
            {
                await seedService.SeedAndSyncPermissionsAsync();
            }
            return app;
        }
    }
}