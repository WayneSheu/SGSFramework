namespace PhysLIMS.API.Extensions;

using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using PhysLIMS.API.Dbcontexts;
using Scalar.AspNetCore;
using SGSFramework.ApiInfrastructure.Filters;
using SGSFramework.ApiInfrastructure.Transformers;
using SGSFramework.AuditLog.Extensions;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Extensions;
using SGSFramework.AuthTokenBucket.Queries.Menuitems;
using SGSFramework.CodeSecurity.Extensions;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.ApiDoc.Extensions;
using SGSFramework.Core.Migrations;
using SGSFramework.Core.SSOs;
using SGSFramework.Identity.Extensions;
using SGSFramework.ModulePlugin.Extensions;
using SGSFramework.Persistent.Extensions;
using SGSFramework.SystemLog.Extensions;
using SGSFramework.VerifyLedger.Extensions;
using System.Reflection;

/// <summary>
/// WebApplicationBuilder 服務註冊依賴注入擴充類別
/// </summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// 配置企業級 API 版本控制與多版本 OpenAPI/Scalar 文件
    /// </summary>
    public static IServiceCollection AddEnterpriseApiVersioningAndDocs(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        // 註冊 v1 OpenAPI 文件規格
        services.AddOpenApi("v1", options =>
        {
            options.ShouldInclude = (description) => string.Equals(description.GroupName, "v1", StringComparison.OrdinalIgnoreCase);
            options.AddOperationTransformer<MenuAttributeTransformer>();
            options.AddDocumentTransformer<DynamicControllerDocumentFilter>();
            options.AddDocumentTransformer<OpenApiSecurityRequirementTransformer>();
        });

        // 註冊 v2 OpenAPI 文件規格
        services.AddOpenApi("v2", options =>
        {
            options.ShouldInclude = (description) => string.Equals(description.GroupName, "v2", StringComparison.OrdinalIgnoreCase);
            options.AddOperationTransformer<MenuAttributeTransformer>();
            options.AddDocumentTransformer<DynamicControllerDocumentFilter>();
            options.AddDocumentTransformer<OpenApiSecurityRequirementTransformer>();
        });

        services.AddAPIDocServices();
        return services;
    }

    /// <summary>
    /// 配置核心資料庫層與 PhysLIMSDbContext
    /// </summary>
    public static IServiceCollection AddCoreDatabaseInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.AddAuditLog(config);
        services.AddPersistentServices();

        services.AddModuleDatabaseWithAudit<PhysLIMSDbContext>(
            configuration: config,
            connectionStringKey: "PersistentSettings:ConnectionStrings",
            schemaName: "core",
            configureOptions: options =>
            {
                options.ReplaceService<IRelationalAnnotationProvider, CustomSqlServerAnnotationProvider>();
                options.ReplaceService<IMigrationsSqlGenerator, CustomSqlServerMigrationsSqlGenerator>()
                       .ConfigureWarnings(warnings =>
                           warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

        services.AddScoped<ICoreDbContext>(sp => sp.GetRequiredService<PhysLIMSDbContext>());
        services.AddScoped<ITokenDbContext>(sp => sp.GetRequiredService<PhysLIMSDbContext>());
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<PhysLIMSDbContext>());

        services.AddGenericIdentityPackage<PhysLIMSDbContext, ApplicationUser, ApplicationRole, Guid>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedAccount = false;
        });

        return services;
    }

    /// <summary>
    /// 配置 Token Bucket 安全身分驗證與授權機制
    /// </summary>
    public static IServiceCollection AddTokenBucketSecurity(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        var scannedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName != null &&
                       (a.FullName.StartsWith("SGS.", StringComparison.Ordinal) ||
                        a.FullName.StartsWith("SGSFramework", StringComparison.Ordinal) ||
                        a.FullName.StartsWith("PhysLIMS", StringComparison.Ordinal) ||
                        a == Assembly.GetEntryAssembly()))
            .Distinct()
            .ToArray();

        services.AddTokenBucketAuthentication<PhysLIMSDbContext, ApplicationUser>(options =>
        {
            options.SecretKey = config["JwtSettings:Secret"]
                            ?? throw new InvalidOperationException("核心資安配置錯誤：未在 appsettings.json 中找到 'JwtSettings:Secret' 設定項。");
            options.Issuer = config["JwtSettings:Issuer"]!;
            options.Audience = config["JwtSettings:Audience"]!;
            options.MaxDeviceCount = 6;
            options.RefreshTokenExpirationDays = 7;
            options.RefreshTokenGracePeriodSeconds = 8;
        },
        scannedAssemblies);

        services.AddSSOServices();
        services.AddAuthorization();

        return services;
    }

    /// <summary>
    /// 配置企業級 CORS 政策
    /// </summary>
    public static IServiceCollection AddEnterpriseCorsPolicy(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.AddCors(options =>
        {
            options.AddPolicy("CorsPolicy", policy =>
            {
                var allowedOrigins = config
                    .GetSection("CorsSettings:AllowedOrigins")
                    .Get<string[]>();

                if (allowedOrigins is null || allowedOrigins.Length == 0 || allowedOrigins.All(string.IsNullOrWhiteSpace))
                {
                    throw new InvalidOperationException("核心資安配置錯誤：未在 appsettings 中找到有效的 'CorsSettings:AllowedOrigins' 設定。");
                }

                policy.WithOrigins(allowedOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        });

        return services;
    }

    /// <summary>
    /// 集中注入生產環境管理者 Seed 服務、帳本驗證與程式碼資安防護
    /// </summary>
    public static IServiceCollection AddEnterpriseSecurityAndSeeders(this IServiceCollection services, IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(config);

        services.AddProductionAdminSeeder(config);
        services.AddLedgerVerificationServices();
        services.AddCodeSecurity(config);

        return services;
    }
}