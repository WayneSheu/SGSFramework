// ==========================================
// 檔案路徑: src/SGSFramework/Infrastructure/SGSFramework.AuthTokenBucket/Extensions/AuthTokenBucketServiceCollectionExtensions.cs
// 架構層級: Infrastructure Layer / Extensions
// ==========================================

namespace SGSFramework.AuthTokenBucket.Extensions;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Configurations;
using SGSFramework.AuthTokenBucket.Repositories;
using SGSFramework.AuthTokenBucket.RuleEngine.Abstractions;
using SGSFramework.AuthTokenBucket.RuleEngine.Rules.Laboratory;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Abstractions.Permissions.Contract;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

public static class AuthTokenBucketServiceCollectionExtensions
{
    private const string DefaultSigningKeyId = "SGS_AuthTokenBucket_SigningKey";

    /// <summary>
    /// 註冊 Token Bucket 身分驗證、JWT 驗證處理器與動態權限服務 (預設主鍵為 Guid)
    /// </summary>
    public static IServiceCollection AddTokenBucketAuthentication<TDbContext, TUser, TRole>(
        this IServiceCollection services,
        Action<AuthTokenBucketOptions> configureOptions,
        params Assembly[] assembliesToScan)
        where TDbContext : DbContext, ITokenDbContext
        where TUser : ApplicationUser, new()
        where TRole : ApplicationRole, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        // 1. 綁定組態選項
        services.Configure(configureOptions);

        var options = new AuthTokenBucketOptions();
        configureOptions(options);

        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            throw new InvalidOperationException("Token Bucket 資安配置錯誤：SecretKey 不得為空。");
        }

        var keyBytes = Encoding.UTF8.GetBytes(options.SecretKey);
        if (keyBytes.Length < 32)
        {
            throw new InvalidOperationException("Token Bucket 資安配置錯誤：SecretKey 長度必須至少為 256 位元 (32 位元組)。");
        }

        // 2. 建立指定 KeyId 的安全性金鑰 (對齊 TokenManager 簽發標頭)
        var securityKey = new SymmetricSecurityKey(keyBytes)
        {
            KeyId = DefaultSigningKeyId
        };

        // 3. 註冊 JwtBearer 身分驗證處理器
        services.AddAuthentication(authOptions =>
        {
            authOptions.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            authOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            authOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            authOptions.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, jwtOptions =>
        {
            jwtOptions.RequireHttpsMetadata = false;
            jwtOptions.SaveToken = true;

            jwtOptions.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = securityKey,
                // 動態金鑰解析器：避免同時設定 IssuerSigningKey 與 IssuerSigningKeys 導致重複金鑰驗證警告
                // 當 Token 缺少 kid 標頭時，自動降級提供預設主金鑰進行簽章驗證
                IssuerSigningKeyResolver = (token, securityToken, kid, validationParameters) => new SecurityKey[] { securityKey },
                ValidateIssuer = !string.IsNullOrWhiteSpace(options.Issuer),
                ValidIssuer = options.Issuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(options.Audience),
                ValidAudience = options.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(options.ClockSkewSeconds < 0 ? 0 : options.ClockSkewSeconds)
            };

            jwtOptions.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("JwtBearerAuthentication");

                    logger.LogError(context.Exception, "JWT 簽章驗證失敗：{Message}", context.Exception.Message);
                    return Task.CompletedTask;
                }
            };
        });

        // 4. 後置鎖定 Authentication Scheme
        services.PostConfigureAll<AuthenticationOptions>(authOptions =>
        {
            authOptions.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            authOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            authOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            authOptions.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
        });

        // 5. 註冊核心服務（解決 ITokenManager 抽象介面無法被 TokenBucketEngine 解析之問題）
        services.AddScoped<ITokenManager, TokenManager>();
        services.AddScoped<TokenManager>(sp => (TokenManager)sp.GetRequiredService<ITokenManager>());
        services.AddScoped<TokenBucketEngine<TUser>>();
        services.AddScoped<ITokenStorageProvider, SqlTokenStorageProvider<TDbContext>>();
        services.AddScoped<IUserRefreshTokenRepository, UserRefreshTokenRepository<TDbContext>>();
        services.AddScoped<IUserRuntimeScopeService, UserRuntimeScopeService>();
        services.AddScoped<IPermissionManagementService, PermissionManagementService<TDbContext, TRole, Guid>>();

        // 6. 動態權限掃描與註冊
        // 註冊實驗室存取與權限驗證服務 
        services.AddScoped<ILaboratoryAccessService, LaboratoryAccessService>();
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

        var registry = new DynamicPermissionRegistry();
        registry.ScanAndRegisterAssemblies(fullAssembliesToScan);
        services.AddSingleton<IPermissionRegistry>(registry);
        // 註冊權限同步服務
        services.AddScoped<IPermissionSeedService>(sp =>
            new PermissionSeedService<TDbContext>(
                sp.GetRequiredService<TDbContext>(),
                sp.GetRequiredService<IPermissionRegistry>(),
                sp.GetRequiredService<ILogger<PermissionSeedService<TDbContext>>>()
            ));

        // 註冊權限授權服務
        services.AddScoped<IPermissionGrantService, PermissionGrantService<TDbContext>>();
        // 由於 UserPermissionRepository 依賴 DbContext（其預設為 Scoped），因此必須註冊為 Scoped
        services.AddScoped<IUserPermissionRepository, UserPermissionRepository>();
        return services;
    }

    /// <summary>
    /// 註冊 Token Bucket 身分驗證與動態權限服務 (預設 TRole 為 ApplicationRole)
    /// </summary>
    public static IServiceCollection AddTokenBucketAuthentication<TDbContext, TUser>(
        this IServiceCollection services,
        Action<AuthTokenBucketOptions> configureOptions,
        params Assembly[] assembliesToScan)
        where TDbContext : DbContext, ITokenDbContext
        where TUser : ApplicationUser, new()
    {
        return services.AddTokenBucketAuthentication<TDbContext, TUser, ApplicationRole>(configureOptions, assembliesToScan);
    }
}