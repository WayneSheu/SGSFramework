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
    using SGSFramework.Core.Abstractions.Entities.Base;
    using SGSFramework.Core.Abstractions.Entities.Identities;
    using SGSFramework.Core.Abstractions.Permissions;
    using System;
    using System.Linq;
    using System.Reflection;

    public static class AuthTokenBucketServiceCollectionExtensions
    {
        /// <summary>
        /// 註冊 Token Bucket 身分驗證與動態權限服務 (預設主鍵為 Guid)
        /// </summary>
        public static IServiceCollection AddTokenBucketAuthentication<TDbContext, TUser, TRole>(
            this IServiceCollection services,
            Action<AuthTokenBucketOptions> configureOptions,
            params Assembly[] assembliesToScan)
            where TDbContext : DbContext, ITokenDbContext
            where TUser : ApplicationUser, new() // <-- 將原本的 IdentityUser<Guid>, IBaseUser 統一改為 ApplicationUser
            where TRole : ApplicationRole, new()
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);

            services.Configure(configureOptions);
            services.AddScoped<TokenManager>();
            services.AddScoped<TokenBucketEngine<TUser>>();
            services.AddScoped<ITokenStorageProvider, SqlTokenStorageProvider<TDbContext>>();
            services.AddScoped<IUserRefreshTokenRepository, UserRefreshTokenRepository<TDbContext>>();
            services.AddScoped<IUserRuntimeScopeService, UserRuntimeScopeService>();

            services.AddScoped<IPermissionManagementService, PermissionManagementService<TDbContext, TRole, Guid>>();

            // 1. 自動彙整全域 AppDomain 中的所有 Assemblies
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

            // 2. 實作 DynamicPermissionRegistry 預先進行全組件掃描 (修復 CS1729)
            var registry = new DynamicPermissionRegistry();
            registry.ScanAndRegisterAssemblies(fullAssembliesToScan);
            services.AddSingleton<IPermissionRegistry>(registry);

            // 3. 註冊 IPermissionSeedService
            services.AddScoped<IPermissionSeedService>(sp =>
                new PermissionSeedService<TDbContext>(
                    sp.GetRequiredService<TDbContext>(),
                    sp.GetRequiredService<IPermissionRegistry>(),
                    fullAssembliesToScan,
                    sp.GetRequiredService<ILogger<PermissionSeedService<TDbContext>>>()
                ));

            return services;
        }

        /// <summary>
        /// 註冊 Token Bucket 身分驗證與動態權限服務 (預設 TRole 為 IdentityRole<Guid>)
        /// </summary>
        public static IServiceCollection AddTokenBucketAuthentication<TDbContext, TUser>(
            this IServiceCollection services,
            Action<AuthTokenBucketOptions> configureOptions,
            params Assembly[] assembliesToScan)
            where TDbContext : DbContext, ITokenDbContext
           where TUser : ApplicationUser, new() // <-- 將原本的 IdentityUser<Guid>, IBaseUser 統一改為 ApplicationUser
        {
            return services.AddTokenBucketAuthentication<TDbContext, TUser, ApplicationRole>(configureOptions, assembliesToScan);
        }
    }
}