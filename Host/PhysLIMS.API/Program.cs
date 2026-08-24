// ==========================================
// 檔案路徑: src/SGSFramework/Host/PhysLIMS.API/Program.cs
// 架構層級: Presentation / Host Layer
// ==========================================

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PhysLIMS.API.Dbcontexts;
using PhysLIMS.API.Extensions;
using PhysLIMS.API.Helpers;
using PhysLIMS.API.Models;
using Scalar.AspNetCore;
using Serilog;
using SGSFramework.ApiInfrastructure.Bootstrappers;
using SGSFramework.ApiInfrastructure.Extensions;
using SGSFramework.ApiInfrastructure.Filters;
using SGSFramework.ApiInfrastructure.Transformers;
using SGSFramework.AuditLog.Extensions;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Extensions;
using SGSFramework.CodeSecurity.Extensions;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.ApiDoc.Extensions;
using SGSFramework.Core.Exceptions;
using SGSFramework.Core.Extensions;
using SGSFramework.Core.Migrations;
using SGSFramework.Core.SSOs;
using SGSFramework.Identity.Extensions;
using SGSFramework.ModulePlugin.Extensions;
using SGSFramework.ModulePlugin.Systems.Controller.Providers;
using SGSFramework.Persistent.Abstractions.ScriptRunners;
using SGSFramework.Persistent.ScriptRunners;
using SGSFramework.SystemLog.Extensions;
using SGSFramework.VerifyLedger.Extensions;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // 設定系統日誌系統
    builder.AddSystemLog();
    Log.Information("Starting WebAPI Application.");

    IConfiguration config = builder.Configuration;

    // 注入 SGSFramework.Core 的服務 
    builder.AddSGSFrameworkCore();

    // 1. 註冊 OpenAPI 與 Scalar API 文件服務
    builder.Services.AddOpenApi("v1", options =>
    {
        options.ShouldInclude = (description) => true;
        options.AddOperationTransformer<MenuAttributeTransformer>();
        options.AddDocumentTransformer<DynamicControllerDocumentFilter>();
    });

    builder.Services.AddAPIDocServices();

    // 2. 主專案資料庫上下文註冊
    builder.Services.AddTransient<IDatabaseInitializer, DatabaseInitializer>();
    //
    builder.Services.AddDbContext<PhysLIMSDbContext>(options =>
    {
        var connectionString = config.GetSection("PersistentSettings:ConnectionStrings")["DefaultConnection"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("未找到 PhysLIMSDbContext 專用的 DefaultConnection 連線字串設定。");
        }

        options.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "core");
        });

        options.ReplaceService<IRelationalAnnotationProvider, CustomSqlServerAnnotationProvider>();
        options.ReplaceService<IMigrationsSqlGenerator, CustomSqlServerMigrationsSqlGenerator>()
               .ConfigureWarnings(warnings =>
                   warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    });
    // 將 ICoreDbContext 的解析轉向現有的 PhysLIMSDbContext 執行個體
    builder.Services.AddScoped<ICoreDbContext>(sp => sp.GetRequiredService<PhysLIMSDbContext>());
    // 將 DbContext 指向相同的 Scoped ApplicationDbContext 實例
    builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<PhysLIMSDbContext>());
    // 3. 泛型 Identity 完整打包註冊
    builder.Services.AddGenericIdentityPackage<PhysLIMSDbContext, ApplicationUser, ApplicationRole, Guid>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
    });

    // 4. 註冊 Controllers 與外掛模組核心
    var mvcBuilder = builder.Services.AddControllers();

    Log.Information("開始掃描並註冊既有動態外掛模組與 DI 服務 (Startup Phase)...");

    // 一鍵包含框架服務與對應 DbContext 的 IModuleStorageStrategy/IModuleRepository 策略注入[cite: 3, 4]
    builder.Services.AddModulePlugin<PhysLIMSDbContext>(config);
    builder.Services.AddControllerScanner<PhysLIMSDbContext>();

    mvcBuilder.ConfigureApplicationPartManager(apm =>
    {
        Log.Information(">>> [ApplicationParts] 目前完成註冊的 Application Parts 總數: {Count}", apm.ApplicationParts.Count);
    });

    // 5. 審計基礎設施服務
    builder.Services.AddAuditLog(config);

    // 6. CORS 設定
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // 7. 全域異常處理
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // 8. Token Bucket 認證、動態 BitMask 權限掃描與 SSO
    var scannedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
        .Where(a => a.FullName != null &&
                   (a.FullName.StartsWith("SGS.") ||
                    a.FullName.StartsWith("PhysLIMS") ||
                    a == Assembly.GetEntryAssembly()))
        .Distinct()
        .ToArray();

    builder.Services.AddTokenBucketAuthentication<PhysLIMSDbContext, ApplicationUser>(options =>
    {
        options.SecretKey = config["Jwt:Secret"]
                        ?? throw new InvalidOperationException("核心資安配置錯誤：未在 appsettings.json 中找到 'Jwt:Secret' 設定項。");
        options.Issuer = config["Jwt:Issuer"]!;
        options.Audience = config["Jwt:Audience"]!;
        options.MaxDeviceCount = 6;
        options.RefreshTokenExpirationDays = 7;
        options.RefreshTokenGracePeriodSeconds = 8;
    },
    scannedAssemblies);

    builder.Services.AddSSOServices();
    builder.Services.AddAuthorization();

    #region 身分驗證配置 (Presentation / Host Layer)
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "Windows";
            options.DefaultChallengeScheme = "Windows";
        });
    }
    else
    {
        var isIISHosted = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APP_POOL_ID")) ||
                          !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANCM_PREFER_USER_STORE"));

        if (isIISHosted)
        {
            Log.Information("偵測到 IIS 託管環境，整合 IIS Native Windows Authentication。");
            RemoveNegotiateServices(builder.Services);

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Server.IISIntegration.IISDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = Microsoft.AspNetCore.Server.IISIntegration.IISDefaults.AuthenticationScheme;
            });

            builder.Services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
            {
                if (options.SchemeMap.ContainsKey("Negotiate"))
                {
                    options.SchemeMap.Remove("Negotiate");
                }

                var negotiateScheme = options.Schemes.FirstOrDefault(s => s.Name == "Negotiate");
                if (negotiateScheme != null)
                {
                    if (options.Schemes is List<Microsoft.AspNetCore.Authentication.AuthenticationSchemeBuilder> schemeList)
                    {
                        schemeList.Remove(negotiateScheme);
                    }
                    Log.Warning("已成功自 AuthenticationOptions 中強制完全抹除 'Negotiate' Scheme。");
                }

                options.DefaultAuthenticateScheme = Microsoft.AspNetCore.Server.IISIntegration.IISDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = Microsoft.AspNetCore.Server.IISIntegration.IISDefaults.AuthenticationScheme;
            });
        }
        else
        {
            Log.Information("偵測到獨立 Kestrel 託管環境，啟用 Native Negotiate 驗證。");

            builder.Services.PostConfigure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
            {
                if (!options.SchemeMap.ContainsKey("Negotiate"))
                {
                    builder.Services.AddSafeNegotiateAuthentication();
                }
            });
        }
    }
    #endregion

    // 9. 註冊生產級 Admin 自動種子服務與其他擴充服務
    builder.Services.AddProductionAdminSeeder(builder.Configuration);
    builder.Services.AddLedgerVerificationServices();
    builder.Services.AddCodeSecurity(config);
    builder.AddDIContainerValidation();

    // 配置 Authentication 並指定 DefaultAuthenticateScheme 與 DefaultChallengeScheme
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:SecretKey"] ?? "YourSuperSecretKeyHere1234567890!")),
            ValidateIssuer = false,
            ValidateAudience = false
        };
    });

    builder.Services.AddAuthorization();

    var app = builder.Build();

    var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
    var bootstrapLogger = loggerFactory.CreateLogger("IisBootstrapExecution");

    try
    {
        bootstrapLogger.LogInformation(">>> 執行前置檢查：IIS 應用程式集區環境變數配置...");
        IisBootstrapTask.Execute(builder.Configuration, bootstrapLogger);
    }
    catch (Exception ex)
    {
        bootstrapLogger.LogCritical(ex, ">>> IIS 應用程式集區環境變數配置失敗。");
        throw;
    }

    // 中間件管道配置
    app.UseExceptionHandler();
    app.UseCors("AllowAll");

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }
    else
    {
        app.UseDeveloperExceptionPage();
    }

    // DB Bootstrapping & EF Core Migration
    var autoMigrate = config.GetValue<bool>("Database:AutoMigrate", true);
    if (app.Environment.IsDevelopment() || autoMigrate)
    {
        using var scope = app.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            logger.LogInformation("Step 1: 開始執行 Bootstrapping 腳本 (使用 Windows/Master 權限)...");
            var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
            await initializer.InitializeDatabaseAsync().ConfigureAwait(false);
            logger.LogInformation("Bootstrapping 腳本執行完成。");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Bootstrapping 失敗，終止啟動。");
            throw;
        }

        string migrationConnectionString = string.Empty;
        try
        {
            migrationConnectionString = config.GetSection("PersistentSettings:ConnectionStrings")["MigrationConnection"]!;
            if (string.IsNullOrWhiteSpace(migrationConnectionString))
            {
                throw new InvalidOperationException("未配置 MigrationConnection。");
            }
            logger.LogInformation("Step 2: 開始執行主專案 DB Migration...");

            var mainDbContextOptions = new DbContextOptionsBuilder<PhysLIMSDbContext>()
                .UseSqlServer(migrationConnectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "core");
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                })
                .Options;

            await using var mainDbContext = new PhysLIMSDbContext(mainDbContextOptions);
            var connection = mainDbContext.Database.GetDbConnection();
            await connection.OpenAsync().ConfigureAwait(false);
            await connection.CloseAsync().ConfigureAwait(false);

            await mainDbContext.Database.MigrateAsync().ConfigureAwait(false);
            logger.LogInformation("主專案 DB Migration 完成。");
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "主專案 DB Migration 發生未預期的嚴重錯誤。");
            throw;
        }
    }

    // 初始化動態控制器與外掛模組
    Log.Information("開始初始化模組系統與動態控制器...");
    await app.InitializeModularSystemAsync().ConfigureAwait(false);
    await app.UseDynamicControllersAsync().ConfigureAwait(false);

    using (var scope = app.Services.CreateScope())
    {
        Log.Information("開始同步與初始化動態 BitMask 權限資料...");
        var permissionSeeder = scope.ServiceProvider.GetRequiredService<IPermissionSeedService>();
        await permissionSeeder.SeedAndSyncPermissionsAsync().ConfigureAwait(false);
    }

    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    var changeProvider = app.Services.GetRequiredService<IDynamicActionDescriptorChangeProvider>();
    Log.Information("觸發 MVC ActionDescriptorCollection 變更通知，刷新 Dynamic Controller 路由...");
    changeProvider.NotifyChanges();

    // 攔截 OpenAPI 請求，強制破除前端快取 (避免 Skeleton Loader 讀取舊檔案卡死)
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers.Expires = "0";
        }
        await next();
    });

    // 註冊 OpenAPI endpoints 與 Scalar 介面
    app.MapOpenApi();

    if (app.Environment.IsDevelopment())
    {
        app.MapScalarApiReference(options =>
        {
            options.WithTitle("PhysLIMS 2.0 API 文件")
                .WithTheme(ScalarTheme.Solarized)
                .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
                .WithOpenApiRoutePattern("/openapi/{documentName}.json");
        });
    }

    app.MapGet("/", async context =>
    {
        context.Response.Redirect("/scalar/v1");
        await Task.CompletedTask;
    });

    await app.RunAsync().ConfigureAwait(false);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}

static void RemoveNegotiateServices(IServiceCollection services)
{
    var negotiateServices = services.Where(sd =>
        sd.ServiceType.FullName?.Contains("Negotiate") == true ||
        sd.ImplementationType?.FullName?.Contains("Negotiate") == true ||
        sd.ImplementationInstance?.GetType().FullName?.Contains("Negotiate") == true
    ).ToList();

    foreach (var service in negotiateServices)
    {
        services.Remove(service);
        Log.Warning("已成功攔截並自 DI 容器中剔除潛在衝突之 Negotiate 服務: {ServiceType}", service.ServiceType.FullName);
    }
}