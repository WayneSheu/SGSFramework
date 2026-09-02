using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Abstractions.Entities.Base;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.Repositories;
using SGSFramework.Identity.Services;

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
           where TRole : IdentityRole<TKey>, IRoleEntity, new() // <--  IRoleEntity 約束
           where TKey : IEquatable<TKey>
        {
            
            // 1. 確保註冊 HttpContextAccessor (SignInManager 必備依賴)
            services.AddHttpContextAccessor();

            // 2. 使用傳入的泛型 TUser 初始化 Identity 核心，並鏈結正確的 TRole
            services.AddIdentityCore<TUser>(setupAction ?? (options =>
            {
                // 預設安全設定
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
            }))
            .AddRoles<TRole>() // 必須先指定 RoleType
            .AddSignInManager<SignInManager<TUser>>()
            .AddUserManager<UserManager<TUser>>()
            .AddRoleManager<RoleManager<TRole>>() // 正確注入 RoleManager<TRole>
            .AddEntityFrameworkStores<TContext>()
            .AddDefaultTokenProviders();

            // 3. 註冊泛型倉儲服務
            services.AddScoped<IGenericIdentityRepository<TUser, TKey>, GenericIdentityRepository<TContext, TUser, TRole, TKey>>();
            // 註冊 UserLabRepository 介面與實作對應
            services.AddScoped<IUserLabRepository, UserLabRepository>();
            // 4. 註冊角色管理服務 (開放泛型與具體泛型介面)
            services.AddScoped(typeof(IRoleManagementService<,>), typeof(RoleManagementService<,>));
            services.AddScoped<IRoleManagementService<TRole, TKey>, RoleManagementService<TRole, TKey>>();

            return services;
        }
    }
}