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
using SGSFramework.Application.Strategies;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Configurations;
using SGSFramework.AuthTokenBucket.Queries.Menuitems;
using SGSFramework.AuthTokenBucket.Repositories;
using SGSFramework.AuthTokenBucket.RuleEngine.Abstractions;
using SGSFramework.AuthTokenBucket.RuleEngine.Rules.Laboratory;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.AuthTokenBucket.Services.Factories;
using SGSFramework.AuthTokenBucket.Services.Strategies;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Menus;
using SGSFramework.Core.Abstractions.Permissions;
using SGSFramework.Core.Abstractions.Permissions.Contract;
using SGSFramework.Core.Abstractions.Permissions.Repositories;
using SGSFramework.Core.Abstractions.Strategies;
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
                // 瀏覽器 WebSocket / SSE 無法自訂 Authorization 標頭，SignalR 改以 access_token 查詢參數傳遞 Token
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(accessToken) &&
                        context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                },
                // 簽章與效期驗證通過後，再確認該裝置工作階段未被撤銷（登出 / 全裝置登出 / 裝置數上限淘汰）
                OnTokenValidated = async context =>
                {
                    var principal = context.Principal;
                    var deviceId = principal?.FindFirst("device_id")?.Value;
                    if (string.IsNullOrWhiteSpace(deviceId))
                    {
                        // 非本系統 TokenBucket 簽發（無裝置綁定）之憑證不在此檢查範圍
                        return;
                    }

                    var userId = principal!.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                                 ?? principal.FindFirst("sub")?.Value
                                 ?? principal.FindFirst("nameid")?.Value;

                    var validator = context.HttpContext.RequestServices.GetRequiredService<ISessionValidator>();
                    if (string.IsNullOrWhiteSpace(userId) ||
                        !await validator.IsSessionActiveAsync(userId, deviceId, context.HttpContext.RequestAborted))
                    {
                        context.Fail("工作階段已撤銷或不存在，請重新登入。");
                    }
                },
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

        services.AddScoped<ITokenStorageProvider, SqlTokenStorageProvider<TDbContext>>();
        services.AddScoped<IUserRefreshTokenRepository, UserRefreshTokenRepository<TDbContext>>();
        services.AddMemoryCache();
        services.AddScoped<ISessionValidator, SessionValidator<TDbContext>>();

        // 工作階段即時通知 (SignalR)：需於 Host 端呼叫 app.MapTokenBucketHubs()
        services.AddSignalR();
        services.AddSingleton<ISessionNotifier, SessionNotifier>();
        
        // 註冊帶有 TDbContext 泛型型態的 PermissionManagementService
        services.AddScoped<IPermissionManagementService, PermissionManagementService<TDbContext>>();
        services.AddScoped<IUserLabPermissionAssignmentService, UserLabPermissionAssignmentService>();

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
      
        // 註冊 Metadata Repository 介面與實作
        services.AddScoped<IPermissionMetadataRepository, PermissionMetadataRepository<TDbContext>>();
        // 由於 UserPermissionRepository 依賴 DbContext（其預設為 Scoped），因此必須註冊為 Scoped
        services.AddScoped<IUserPermissionRepository, UserPermissionRepository>();
        services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
        services.AddScoped<IUserLabPermissionRepository, UserLabPermissionRepository<TDbContext>>();

        // 註冊動態權限解析策略
        services.AddScoped<IPermissionResolver, DefaultPermissionResolver>();
        services.AddScoped<TokenBucketEngine<TUser>>();
        // 確保將 PrimaryLabPermissionStrategy 與 SecondaryLabPermissionStrategy 註冊至 DI 容器
        services.AddScoped<ILabPermissionStrategy, PrimaryLabPermissionStrategy>();
        services.AddScoped<ILabPermissionStrategy, AdjunctLabPermissionStrategy>();


        // 註冊獨立的動態選單種子服務
        services.AddScoped<IMenuSeedService, MenuSeedService<TDbContext>>();
        // 註冊所有選單解析策略實作
        services.AddScoped<IMenuResolutionStrategy, DatabaseMenuResolutionStrategy>();
        // 註冊選單策略工廠
        services.AddScoped<IMenuStrategyFactory, MenuStrategyFactory>();
        // 角色有效權限（納入執行期權限檢查，排除停用角色）與權限異動通知（清快取 + SignalR）
        services.AddScoped<IRoleEffectivePermissionProvider, RoleEffectivePermissionProvider>();
        services.AddScoped<IPermissionChangeNotifier, PermissionChangeNotifier>();
        services.AddScoped<IRoleAuditService, RoleAuditService>();
        services.AddScoped<IRoleGovernanceService, RoleGovernanceService>();
        // 註冊執行期作用域服務
        services.AddScoped<ILabFunctionScopeService, LabFunctionScopeService>();
        services.AddScoped<IUserRuntimeScopeService, UserRuntimeScopeService>();
        // 註冊權限遮罩服務
        services.AddScoped<IPermissionBitmaskService, PermissionBitmaskService>();
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