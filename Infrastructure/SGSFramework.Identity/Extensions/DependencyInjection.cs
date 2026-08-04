using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.Repositories;

namespace SGSFramework.Identity.Extensions
{
    /// <summary>
    /// 提供泛型化 Identity 套件的注入擴充方法
    /// </summary>
    public static class DependencyInjection
    {
        /// <summary>
        /// 註冊擴充的泛型 Identity 套件服務
        /// </summary>
        public static IServiceCollection AddGenericIdentityPackage<TContext, TUser, TRole, TKey>(
            this IServiceCollection services,
            Action<IdentityOptions>? setupAction = null)
            where TContext : DbContext
            where TUser : IdentityUser<TKey>, IBaseUser, new()
            where TRole : IdentityRole<TKey>, new()
            where TKey : IEquatable<TKey>
        {
            // 1. 註冊 ASP.NET Core Identity 核心服務並關聯外部 DbContext
            var identityBuilder = services.AddIdentityCore<TUser>(setupAction ?? (options =>
            {
                // 預設安全設定
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            }));

            identityBuilder
                .AddRoles<TRole>()
                .AddEntityFrameworkStores<TContext>()
                .AddDefaultTokenProviders();

            // 2. 註冊泛型倉儲服務
            services.AddScoped<IGenericIdentityRepository<TUser, TKey>, GenericIdentityRepository<TContext, TUser, TRole, TKey>>();

            return services;
        }
    }
}
