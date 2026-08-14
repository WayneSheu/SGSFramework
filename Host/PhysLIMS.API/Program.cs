// ==========================================
// 檔案路徑: PhysLIMS.API/Program.cs
// ==========================================

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PhysLIMS.API.Dbcontexts;
using PhysLIMS.API.Extensions;
using PhysLIMS.API.Helpers;
using PhysLIMS.API.Models;
using Scalar.AspNetCore;
using Serilog;
using SGSFramework.ApiInfrastructure.Filters;
using SGSFramework.AuditLog.Extensions;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Extensions;
using SGSFramework.CodeSecurity.Extensions;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.ApiDoc.Extensions;
using SGSFramework.Core.Exceptions;
using SGSFramework.Core.Extensions;
using SGSFramework.Core.SSOs;
using SGSFramework.Identity.Extensions;
using SGSFramework.ModulePlugin.Extensions;
using SGSFramework.ModulePlugin.Systems.Controller.Providers;
using SGSFramework.Persistent.Abstractions.ScriptRunners;
using SGSFramework.Persistent.ScriptRunners;
using SGSFramework.SystemLog.Extensions;
using SGSFramework.VerifyLedger.Extensions;
using System.Reflection;
try
{
    var builder = WebApplication.CreateBuilder(args);

    // 設定系統日誌系統
    builder.AddSystemLog();
    Log.Information("Starting WebAPI Application.");

    IConfiguration config = builder.Configuration;

    // 注入 SGSFramework.Core 的服務 
    builder.AddSGSFrameworkCore();

    // 1. 註冊自訂 Scalar API 文件服務
    builder.Services.AddOpenApi("v1", options =>
    {
        options.ShouldInclude = (description) => true;
        options.AddDocumentTransformer<DynamicControllerDocumentTransformer>();
    });
    // 註冊 Scalar API 文件服務
    builder.Services.AddAPIDocServices();

    // 2. 主專案資料庫上下文註冊 (配置 SQL Server / Schema)
    //註冊 DatabaseInitializer 服務
    builder.Services.AddTransient<IDatabaseInitializer, DatabaseInitializer>();

    builder.Services.AddDbContext<PhysLIMSDbContext>(options =>
    {
        //
        var connectionString = config.GetSection("PersistentSettings:ConnectionStrings")["DefaultConnection"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("未找到 PhysLIMSDbContext 專用的 DefaultConnection 連線字串設定。");
        }

        options.UseSqlServer(
            connectionString,
            sqlOptions => sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "core_"));
    });

    // 3. 泛型 Identity 完整打包註冊
    // 整合 AddGenericIdentityPackage 一步到位完成 IdentityCore, Roles, Stores, TokenProviders 
    // 以及 IGenericIdentityRepository 與 IRoleManagementService 的 DI 註冊
    // 將泛型參數改為: PhysLIMSDbContext, ApplicationUser, IdentityRole<Guid>, Guid
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

    // 4. 註冊 Controllers 與外掛模組核心 (Startup Phase)
    var mvcBuilder = builder.Services.AddControllers();

    Log.Information("開始掃描並註冊既有動態外掛模組與 DI 服務 (Startup Phase)...");

    // 一鍵封裝註冊模組外掛系統、倉儲、ChangeProvider 與 BackgroundServices
    builder.Services.AddModulePlugin<PhysLIMSDbContext>(config);

    // 註冊 Controller 掃描器服務
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

    //Token Bucket 認證（同步將使用者實體改為 ApplicationUser）
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
        })
        .AddScheme<FakeWindowsAuthOptions, FakeWindowsAuthHandler>("Windows", options =>
        {
            options.DefaultDomain = "CORP";
            options.DefaultUserName = "wayne";
            options.DefaultRole = "Domain Admins";
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

    // -------------------------------------------------------------
    // 建立 WebApplication (此後 ServiceCollection 鎖定為 Read-Only)
    // -------------------------------------------------------------
    var app = builder.Build();

    // ==========================================
    // 中間件管道配置 (Configure HTTP Pipeline)
    // ==========================================
    app.UseExceptionHandler();
    app.UseCors("AllowAll");
    app.UseHttpsRedirection();

    // 僅於開發環境/部署自動化階段執行 DB 結構初始化與更新
    if (app.Environment.IsDevelopment())
    {
        using (var scope = app.Services.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
           
            // 1. 執行 Bootstrapping (建立 DB、app_sgs_user、deploy_sgs_user)
            try
            {
                logger.LogInformation("Step 1: 開始執行 Bootstrapping 腳本 (使用 Windows/Master 權限)...");
                var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
                await initializer.InitializeDatabaseAsync();
                logger.LogInformation("Bootstrapping 腳本執行完成。");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Bootstrapping 失敗，終止啟動。");
                throw;
            }

            // 2. 取得 DDL 專用連線字串 (deploy_sgs_user)
            var migrationConnectionString = config.GetSection("PersistentSettings:ConnectionStrings")["MigrationConnection"];
            if (string.IsNullOrWhiteSpace(migrationConnectionString))
            {
                throw new InvalidOperationException("未配置 PersistentSettings:ConnectionStrings:MigrationConnection 設定。");
            }

            // 3. 執行主專案 EF Core Migration
            try
            {
                logger.LogInformation("Step 2: 開始執行主專案 DB Migration (使用 deploy_sgs_user)...");
                var mainDbContextOptions = new DbContextOptionsBuilder<PhysLIMSDbContext>()
                    .UseSqlServer(migrationConnectionString, sqlOptions => sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "core"))
                    .Options;

                await using var mainDbContext = new PhysLIMSDbContext(mainDbContextOptions);
                await mainDbContext.Database.MigrateAsync();
                logger.LogInformation("主專案 DB Migration 完成。");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "主專案 DB Migration 失敗。");
                throw;
            }

            // 4. 執行部署後 Plugin Modules DB Migration
            try
            {
                logger.LogInformation("Step 3: 開始掃描並執行部署後 Plugin Modules DB Migration...");
                //var pluginMigrationRunner = scope.ServiceProvider.GetRequiredService<IPluginMigrationRunner>();
                //await pluginMigrationRunner.ExecutePluginMigrationsAsync(migrationConnectionString);
                
                await app.InitializeModularSystemAsync();
                
                logger.LogInformation("所有 Plugin Modules DB Migration 執行完畢。");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Plugin Modules DB Migration 失敗。");
                throw;
            }
        }
    }



    // Step 2. 初始化模組系統與動態控制器
    Log.Information("開始初始化模組系統與動態控制器...");
    await app.InitializeModularSystemAsync();
    await app.UseDynamicControllersAsync();

    // Step 3. 執行動態權限自動同步與 Seed
    using (var scope = app.Services.CreateScope())
    {
        Log.Information("開始同步與初始化動態 BitMask 權限資料...");
        var permissionSeeder = scope.ServiceProvider.GetRequiredService<IPermissionSeedService>();
        await permissionSeeder.SeedAndSyncPermissionsAsync();
    }

    // Step 4. 安全與授權管道
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    // Step 5. 註冊 Controller 路由點
    app.MapControllers();

    // Step 6. 觸發 ActionDescriptor 變更通知，刷新 Dynamic Controller 路由
    var changeProvider = app.Services.GetRequiredService<IDynamicActionDescriptorChangeProvider>();
    Log.Information("觸發 MVC ActionDescriptorCollection 變更通知，刷新 Dynamic Controller 路由...");
    changeProvider.NotifyChanges();

    // Step 7. OpenAPI 與 Scalar 文件映射
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("PhysLIMS 2.0 API 文件")
               .WithTheme(ScalarTheme.Solarized)
               .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
               .WithOpenApiRoutePattern("/openapi/{documentName}.json");
    });

    app.MapGet("/", async context =>
    {
        context.Response.Redirect("/scalar/v1");
        await Task.CompletedTask;
    });

    await app.RunAsync();
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