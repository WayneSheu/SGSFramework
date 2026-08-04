using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Configurations;
using SGSFramework.AuthTokenBucket.Repositories;
using SGSFramework.AuthTokenBucket.Servers;
using System;
using System.Collections.Generic;
using System.Text;

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

            //注入強型別組態配置
            services.Configure(configureOptions);

            //注入核心管理元件
            services.AddScoped<TokenManager>();

            //將對應的強型別引擎綁定進 DI 容器，確保其在與 TDbContext 相同的 Scope 下被建構
            services.AddScoped<TokenBucketEngine<TUser>>();

            //將儲存層介面與泛型實作綁定
            services.AddScoped<ITokenStorageProvider, SqlTokenStorageProvider<TDbContext>>();
            services.AddScoped<IUserRefreshTokenRepository, UserRefreshTokenRepository<TDbContext>>();

            return services;
        }
    }
}


//主專案 Program.cs 流暢流暢導入範例
//雙泛型擴充方法，完美對齊 DbContext 與 User 實體
// PhysLIMSDBContext : BaseIdentityDbContext<IdentityUser,IdentityRole,string, PhysLIMSDBContext>, ILogDbContext, ITokenDbContext
// C#
//builder.Services.AddTokenBucketAuthentication<PhysLIMSDBContext, IdentityUser>(options =>
//{
//    options.SecretKey = builder.Configuration["Jwt:Secret"]!;
//    options.Issuer = builder.Configuration["Jwt:Issuer"]!;
//    options.Audience = builder.Configuration["Jwt:Audience"]!;
//    options.MaxDeviceCount = 6;
//    options.RefreshTokenExpirationDays = 7;
//    options.RefreshTokenGracePeriodSeconds = 8;
//});


