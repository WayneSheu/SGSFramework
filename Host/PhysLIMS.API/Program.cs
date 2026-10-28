// Path: src/SGSFramework/Host/PhysLIMS.API/Program.cs
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Web.Administration;
using PhysLIMS.API.Dbcontexts;
using PhysLIMS.API.Extensions;
using Scalar.AspNetCore;
using Serilog;
using SGSFramework.ApiInfrastructure.Bootstrappers;
using SGSFramework.ApiInfrastructure.Configurations;
using SGSFramework.ApiInfrastructure.DependencyInjection;
using SGSFramework.ApiInfrastructure.Middlewares;
using SGSFramework.AuthTokenBucket.Extensions;
using SGSFramework.AuthTokenBucket.Queries.Menuitems;
using SGSFramework.Core.Configurations;
using SGSFramework.Core.Exceptions;
using SGSFramework.Core.Extensions;
using SGSFramework.Core.Persistence;
using SGSFramework.Core.Security.DataProtection;
using SGSFramework.Core.Security.FieldEncryption;
using SGSFramework.ModulePlugin.Extensions;
using SGSFramework.ModulePlugin.Systems.Controller.Providers;
using SGSFramework.Persistent.Repositories.Generally;
using SGSFramework.Scheduler.Extensions;
using SGSFramework.SPAModulePlugin.Application.Queries;
using SGSFramework.SystemLog.Extensions;
using SGSFramework.SystemLog.Middlewares;

try
{
    var builder = WebApplication.CreateBuilder(args);
    // ----------------------------------------------------
    // 【建置期】 WebRootPath，確保 Debug/Production 皆優先指向 AppContext/wwwroot
    // ----------------------------------------------------
    var currentBinWebRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    if (Directory.Exists(currentBinWebRoot))
    {
        builder.Environment.WebRootPath = currentBinWebRoot;
    }

    // 【機密組態】ISO 27001 / 資安法：密碼、金鑰、連線字串不得寫入原始碼與版控中的 appsettings*.json
    // 載入順序 (後者覆寫前者)：appsettings.json → appsettings.{Env}.json → claudesettings.json (受保護、不入版控) → 環境變數
    // 環境變數範例：PersistentSettings__ConnectionStrings__DefaultConnection、JwtSettings__SecretKey、HmsSettings__SharedSecret
    builder.Configuration
        .AddJsonFile("claudesettings.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();

    IConfiguration config = builder.Configuration;
    builder.AddSGSFrameworkCore();

    // 系統配置管理：集中註冊強型別 Options (DataAnnotations + ValidateOnStart)
    builder.Services.AddSystemConfiguration(config);

    // =========================================================================
    // 💡 關鍵架構修正：將資料庫基礎設施與 PhysLIMSDbContext 優先註冊
    // 確保 IQueryRepository<> 所依賴的 DbContext 抽象與具體實作在 DI 容器中先行就緒
    // =========================================================================
    builder.Services.AddCoreDatabaseInfrastructure(config);
    builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<PhysLIMSDbContext>());
    // 🌟 【關鍵補遺】明確註冊開放泛型查詢倉儲，讓 SystemLog 模組能透過介面操作資料庫
    builder.Services.AddScoped(typeof(IQueryRepository<>), typeof(QueryRepository<>));

    // 於資料庫與 Repository 依賴就緒後，再初始化系統日誌與相關查詢服務
    builder.AddSystemLog();
    Log.Information("Starting WebAPI Application.");

    // 【安全性強化】Data Protection 金鑰環 (同時保護登入 Cookie 與 [Encrypted] 個資欄位，遺失 = 個資無法解密)
    // 依執行環境自動選擇保護策略：地端 Windows → DPAPI；已設定憑證 (多台 / 災備 / 雲端 / 容器) → Certificate；
    // 無法確保金鑰加密且持久保存時，正式環境拒絕啟動。可用 DataProtection__Mode 明確指定。
    var dataProtectionSetup = builder.Services.AddHostedDataProtection(config, builder.Environment);
    Log.Information("Data Protection 金鑰保護：{Mode} ({Reason})，金鑰路徑：{KeysPath}",
        dataProtectionSetup.Mode, dataProtectionSetup.Reason, dataProtectionSetup.KeysPath);

    // 2. 註冊多版本 API 控制與 OpenAPI/Scalar 文件 (v1, v2)
    builder.Services.AddEnterpriseApiVersioningAndDocs();

    // 4. 控制器、動態外掛模組與組件掃描註冊
    builder.Services.AddControllerInfrastructure(config);
    builder.Services.AddCustomApiBehavior();
    builder.Services.AddModulePlugin<PhysLIMSDbContext>(config);
    builder.Services.AddControllerScanner<PhysLIMSDbContext>();

    // 5. 跨域政策、例外處理與 Token Bucket 身分驗證
    builder.Services.AddEnterpriseCorsPolicy(config);
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // 排程框架 (Hangfire)：以系統帳號 (M2M) 執行資料庫備份、定期權限覆核；
    // 須早於 AddTokenBucketSecurity，使其掃描權限時已載入排程組件的控制器
    builder.Services.AddSGSScheduler(config);
    builder.Services.AddTokenBucketSecurity(config);

    // ====================================================
    // 【動態部署模式設定】讀取 SPA 託管開關 (跨域來源統一由 CorsSettings 管理，見 AddEnterpriseCorsPolicy)
    // ====================================================
    var enableSpaHosting = config
        .GetValidatedOptions<DeploymentOptions>(DeploymentOptions.SectionName)
        .EnableSpaHosting;

    // 6. 註冊 MediatR 服務與 CQRS Handlers 掃描
    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssemblies(
         typeof(GetFullMenuTreeQueryHandler).Assembly,
         typeof(GetAuthorizedSPAModulesQueryHandler).Assembly //掃描 SPAModulePlugin 的 Handlers
     );
    });

    #region 身分驗證環境配置 (IIS / Kestrel)
    var isIISHosted = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APP_POOL_ID")) ||
                      !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANCM_PREFER_USER_STORE"));

    if (isIISHosted)
    {
        Log.Information("偵測到 IIS 託管環境，整合 IIS Native Windows Authentication。");
    }

    builder.Services.AddCustomAuthentication(isIISHosted);
    #endregion

    // 7. 資安防護、Seed 服務與 DI 容器驗證
    builder.Services.AddEnterpriseSecurityAndSeeders(config);
    builder.AddDIContainerValidation();

    // 強制移除 SGSFramework 核心注入的 Negotiate 啟動防呆檢查
    var negotiateFilters = builder.Services
        .Where(s => s.ServiceType.Name == "IStartupFilter" &&
                    s.ImplementationType?.Name == "NegotiateOptionsValidationStartupFilter")
        .ToList();

    foreach (var filter in negotiateFilters)
    {
        builder.Services.Remove(filter);
    }

    var app = builder.Build();

    // 綁定 [Encrypted] 欄位加解密金鑰環 (須早於任何 Seeder / Migration 的資料庫存取)
    app.UseFieldEncryption();

    // 8. IIS 環境變數配置執行
    var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
    var bootstrapLogger = loggerFactory.CreateLogger("IisBootstrapExecution");

    // 僅地端 Windows 主機執行 IIS 自動設定；雲端 Linux / 容器沒有 IIS (WindowsIdentity 會拋出 PlatformNotSupportedException)
    if (OperatingSystem.IsWindows() && !DataProtectionHostingExtensions.IsRunningInContainer())
    {
        try
        {
            IisBootstrapTask.Execute(
                app.Services.GetRequiredService<IOptions<IisHostingOptions>>().Value,
                bootstrapLogger);
        }
        catch (Exception ex)
        {
            bootstrapLogger.LogCritical(ex, ">>> IIS 應用程式集區環境變數配置失敗。");
            throw;
        }
    }
    else
    {
        bootstrapLogger.LogInformation("非 Windows 地端主機 (雲端 / 容器)，略過 IIS 應用程式集區自動設定。");
    }

    // ====================================================
    // 中間件管道配置 (Middleware Pipeline Execution Order)
    // ====================================================
    app.UseExceptionHandler();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }
    else
    {
        app.UseDeveloperExceptionPage();
    }

    // 1. 優先執行資料庫 Migration 與基礎 Schema 建置
    await app.ExecuteStartupSeedersAsync().ConfigureAwait(false);

    // 2. 動態控制器模組載入與外掛初始化（必須在 MapControllers 之前完成 Assembly 載入）
    try
    {
        Log.Information("正在初始化動態外掛系統，執行目錄：{BaseDir}", AppContext.BaseDirectory);
        await app.InitializeModularSystemAsync().ConfigureAwait(false);
        await app.UseDynamicControllersAsync().ConfigureAwait(false);
    }
    catch (Exception ex)
    {
        Log.Error(ex, ">>> 動態外掛模組初始化失敗！");
        throw;
    }

    // 3. 安全授權與跨域中間件 (順序嚴格對齊：CORS -> 認證 -> 授權)
    app.UseCorrelationIdContext(); // 同一請求的日誌共用 CorrelationId（日誌查詢的「追蹤碼」）
    app.UseCors(CorsSettingsOptions.PolicyName);
    app.UseMiddleware<CorsLoggingMiddleware>();

    // ====================================================
    // 【企業級架構分流】依據 EnableSpaHosting 決定執行模式
    // ====================================================
    if (enableSpaHosting)// 启用「同站台共用部署」：後端同時託管靜態資源與雙 SPA 路由。
    {
        Log.Information("【架構模式】啟用「同站台共用部署」：後端同時託管靜態資源與雙 SPA 路由。");

        // 7. 啟用全域靜態檔案與 MIME 支援 (負責根目錄與 Vue 3)
        app.UseEnterpriseStaticFiles();

        // 具體實體檔案提供者對應 /blazor 路由
        var blazorRootPath = Path.Combine(app.Environment.WebRootPath, "blazor");
        if (Directory.Exists(blazorRootPath))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(blazorRootPath),
                RequestPath = "/blazor"
            });
        }

        // 根目錄斜線校正
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.Equals("/blazor", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect("/blazor/", permanent: true);
                return;
            }
            await next().ConfigureAwait(false);
        });

        // 顯式啟用路由中間件
        app.UseRouting();
        app.UseRouteContextAnalysis(); // 將模組 / 操作名稱注入日誌上下文（需在 UseRouting 之後）
        app.UseAuthentication();
        app.UseAuthorization();

        // 4. OpenAPI 快取控制中間件
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
                context.Response.Headers.Pragma = "no-cache";
                context.Response.Headers.Expires = "0";
            }
            await next().ConfigureAwait(false);
        });

        // 5. 映射 API Controllers 與文件端點
        app.MapOpenApi();
        app.MapCustomScalarApiReference();
        app.MapControllers();
        app.MapTokenBucketHubs(); // 工作階段即時通知 (SignalR)
        app.MapSGSSchedulerDashboard(); // Hangfire Dashboard (Scheduler:Dashboard:Enabled 時)

        var changeProvider = app.Services.GetRequiredService<IDynamicActionDescriptorChangeProvider>();
        changeProvider.NotifyChanges();

        // 6. 掛載雙 SPA (Blazor WASM / Vue 3) 後備路由
        app.MapBlazorSpaFallback();
        app.MapProtectedSpaFallback(app.Environment, "index.html");
    }
    else//前後端分開部署
    {
        Log.Information("【架構模式】啟用「獨立部署 (前後端分離)」：後端僅提供 WebAPI 與 OpenAPI 服務。");

        app.UseRouting();
        app.UseRouteContextAnalysis(); // 將模組 / 操作名稱注入日誌上下文（需在 UseRouting 之後）
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapOpenApi();
        app.MapCustomScalarApiReference();
        app.MapControllers();
        app.MapTokenBucketHubs(); // 工作階段即時通知 (SignalR)
        app.MapSGSSchedulerDashboard(); // Hangfire Dashboard (Scheduler:Dashboard:Enabled 時)

        //當使用者造訪根目錄 (/) 時，自動重新導向至 Scalar API 文件頁面
        app.MapGet("/", async context =>
        {
            context.Response.Redirect("/scalar/v1", permanent: false);
            await Task.CompletedTask;
        });

        var changeProvider = app.Services.GetRequiredService<IDynamicActionDescriptorChangeProvider>();
        changeProvider.NotifyChanges();
    }

    await app.RunAsync().ConfigureAwait(false);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
    Environment.ExitCode = -1;
    throw;
}
finally
{
    Log.CloseAndFlush();
}