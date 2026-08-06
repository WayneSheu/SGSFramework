using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Configurations;
using SGSFramework.AuthTokenBucket.Repositories;
using SGSFramework.AuthTokenBucket.Servers;
using SGSFramework.Core.Abstractions.DbContexts;

namespace SGSFramework.AuthTokenBucket.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 企業級雙票安全水桶流暢註冊進入點
        /// </summary>
        public static IServiceCollection AddTokenBucketAuthentication<TDbContext, TUser>(
                this IServiceCollection services,
                Action<AuthTokenBucketOptions> configureOptions)
                where TDbContext : DbContext, ITokenDbContext
                where TUser : IdentityUser, new()
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configureOptions);

            // 1. 注入強型別組態配置
            services.Configure(configureOptions);

            // 2. 注入核心管理元件
            services.AddScoped<TokenManager>();

            // 3. 將對應的強型別引擎綁定進 DI 容器，確保其在與 TDbContext 相同的 Scope 下被建構
            services.AddScoped<TokenBucketEngine<TUser>>();

            // 4. 將儲存層介面與泛型實作綁定
            services.AddScoped<ITokenStorageProvider, SqlTokenStorageProvider<TDbContext>>();
            services.AddScoped<IUserRefreshTokenRepository, UserRefreshTokenRepository<TDbContext>>();

            // 5. 註冊使用者執行期上下文切換服務 (Scoped 級別，生命週期綁定至每個 Request Scope)
            services.AddScoped<IUserRuntimeScopeService, UserRuntimeScopeService>();

            return services;
        }
    }
}