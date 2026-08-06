using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PhysLIMS.API.Dbcontexts;
using PhysLIMS.API.Extensions;
using PhysLIMS.API.Helpers;
using PhysLIMS.API.Models;
using Scalar.AspNetCore;
using Serilog;
using SGSFramework.ApiInfrastructure.Extensions;
using SGSFramework.ApiInfrastructure.Filters;
using SGSFramework.AuditLog.Extensions;
using SGSFramework.AuthTokenBucket.Extensions;
using SGSFramework.CodeSecurity.Extensions;
using SGSFramework.Core.ApiDoc.Extensions;
using SGSFramework.Core.Exceptions;
using SGSFramework.Core.Extensions;
using SGSFramework.Core.SSOs;
using SGSFramework.ModulePlugin.Extensions;
using SGSFramework.ModulePlugin.Systems.Controller.Providers;
using SGSFramework.Persistent.Extensions;
using SGSFramework.SystemLog.Extensions;
using SGSFramework.VerifyLedger.Extensions;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // 設定系統日誌系統
    builder.AddSystemLog();
    Log.Information("Starting WebAPI Application.");

    IConfiguration config = builder.Configuration;

    //注入SGSFramework.Core 的服務 
    builder.AddSGSFrameworkCore();

    // 1. 註冊自訂 Scalar API 文件服務
    builder.Services.AddOpenApi("v1", options =>
    {
        options.ShouldInclude = (description) => true;
        options.AddDocumentTransformer<DynamicControllerDocumentTransformer>();
    });
    builder.Services.AddAPIDocServices();

    // 2. 主專案資料庫上下文與 Identity 註冊
    builder.Services.AddIdentityDbContextWithOptions<PhysLIMSDbContext, IdentityUser, IdentityRole, string>(
        config,
        schema: "dbo",
        configureIdentity: options =>
        {
            options.Password.RequireDigit = true;
            options.SignIn.RequireConfirmedAccount = false;
        }
    );

    // 3. 註冊 Controllers 與外掛模組核心 (Startup Phase)
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

    // 4. 注入 SGSFramework.Core 與審計基礎設施服務
    builder.AddSGSFrameworkCore();
    builder.Services.AddAuditLog(config);

    // 5. CORS 設定
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // 6. 全域異常處理與 Identity 核心
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    builder.Services.AddIdentityCore<IdentityUser>(options => { })
                   .AddEntityFrameworkStores<PhysLIMSDbContext>();

    // 7. Token Bucket 認證與 SSO
    builder.Services.AddTokenBucketAuthentication<PhysLIMSDbContext, IdentityUser>(options =>
    {
        options.SecretKey = config["Jwt:Secret"]
                        ?? throw new InvalidOperationException("核心資安配置錯誤：未在 appsettings.json 中找到 'Jwt:Secret' 設定項。");
        options.Issuer = config["Jwt:Issuer"]!;
        options.Audience = config["Jwt:Audience"]!;
        options.MaxDeviceCount = 6;
        options.RefreshTokenExpirationDays = 7;
        options.RefreshTokenGracePeriodSeconds = 8;
    });

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

    // 8. 其他擴充服務
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

    // Step 1. 執行主專案 DB Migration
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetService<PhysLIMSDbContext>();
        if (context != null)
        {
            Log.Information("開始執行主專案資料庫遷移...");
            context.Database.Migrate();
        }
        else
        {
            Log.Error(">>> 無法取得主專案的 DbContext 實例");
        }
    }

    // Step 2. 初始化模組系統與動態控制器
    Log.Information("開始初始化模組系統與動態控制器...");
    await app.InitializeModularSystemAsync();

    await app.UseDynamicControllersAsync();

    // Step 3. 安全與授權管道
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();

    // Step 4. 註冊 Controller 路由點
    app.MapControllers();

    // Step 5. 觸發 ActionDescriptor 變更通知，刷新 Dynamic Controller 路由
    var changeProvider = app.Services.GetRequiredService<IDynamicActionDescriptorChangeProvider>();
    Log.Information("觸發 MVC ActionDescriptorCollection 變更通知，刷新 Dynamic Controller 路由...");
    changeProvider.NotifyChanges();

    // Step 6. OpenAPI 與 Scalar 文件映射
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