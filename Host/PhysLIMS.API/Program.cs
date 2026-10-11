// Path: src/SGSFramework/Host/PhysLIMS.API/Program.cs
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Web.Administration;
using PhysLIMS.API.Dbcontexts;
using PhysLIMS.API.Extensions;
using Scalar.AspNetCore;
using Serilog;
using SGSFramework.ApiInfrastructure.Bootstrappers;
using SGSFramework.ApiInfrastructure.DependencyInjection;
using SGSFramework.ApiInfrastructure.Middlewares;
using SGSFramework.AuthTokenBucket.Queries.Menuitems;
using SGSFramework.Core.Exceptions;
using SGSFramework.Core.Extensions;
using SGSFramework.ModulePlugin.Extensions;
using SGSFramework.ModulePlugin.Systems.Controller.Providers;
using SGSFramework.SPAModulePlugin.Application.Queries;
using SGSFramework.SystemLog.Extensions;

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

    // 1. 初始化系統日誌與核心框架
    builder.AddSystemLog();
    Log.Information("Starting WebAPI Application.");

    IConfiguration config = builder.Configuration;
    builder.AddSGSFrameworkCore();

    // 【安全性強化】將 Data Protection 金鑰持久化至專案實體資料夾，避免 IIS App Pool 回收導致登入失效
    var keysFolder = Path.Combine(builder.Environment.ContentRootPath, "keys");
    if (!Directory.Exists(keysFolder))
    {
        Directory.CreateDirectory(keysFolder);
    }

    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(keysFolder))
        .SetApplicationName("PhysLIMS.Enterprise");

    // 2. 註冊多版本 API 控制與 OpenAPI/Scalar 文件 (v1, v2)
    builder.Services.AddEnterpriseApiVersioningAndDocs();

    // 3. 註冊資料庫基礎設施與 PhysLIMSDbContext 組合 (含 AuditLog 與 Persistent)
    builder.Services.AddCoreDatabaseInfrastructure(config);

    // 4. 控制器、動態外掛模組與組件掃描註冊
    builder.Services.AddControllerInfrastructure(config);
    builder.Services.AddCustomApiBehavior();
    builder.Services.AddModulePlugin<PhysLIMSDbContext>(config);
    builder.Services.AddControllerScanner<PhysLIMSDbContext>();

    // 5. 跨域政策、例外處理與 Token Bucket 身分驗證
    builder.Services.AddEnterpriseCorsPolicy(config);
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddTokenBucketSecurity(config);

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

    // 8. IIS 環境變數配置執行
    var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
    var bootstrapLogger = loggerFactory.CreateLogger("IisBootstrapExecution");

    try
    {
        IisBootstrapTask.Execute(config, bootstrapLogger);
    }
    catch (Exception ex)
    {
        bootstrapLogger.LogCritical(ex, ">>> IIS 應用程式集區環境變數配置失敗。");
        throw;
    }

    // ====================================================
    // 中間件管道配置 (Middleware Pipeline Execution Order)
    // 完全對齊伺服器版 (修正動態路由刷新順序與 SPA 管道隔離)
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
    await app.ExecuteStartupSeedersAsync(config).ConfigureAwait(false);

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
    app.UseCors("CorsPolicy");
    app.UseMiddleware<CorsLoggingMiddleware>();
    // 7. 啟用靜態檔案與 MIME 支援
    //掛載 Blazor 框架專屬資源與 MIME 設定
    app.UseEnterpriseStaticFiles();

    // 顯式啟用 Blazor Framework 靜態資產對應 (自動處理 _framework 下所有 .wasm, .js, .dll)
    app.UseBlazorFrameworkFiles("/blazor");

    // 【修正 2】在路由前置攔截並校正 /blazor 網址斜線
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.Equals("/blazor", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Redirect("/blazor/", permanent: true);
            return;
        }
        await next().ConfigureAwait(false);
    });

    // 顯式啟用路由中間件 (必須在 UseAuthentication 之前或緊接其後)
    app.UseRouting();
    
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

    // 5.  映射 API Controllers 與文件端點，建立 EndpointDataSource 路由樹
    app.MapOpenApi();
    app.MapCustomScalarApiReference();
    app.MapControllers();

    // 6. 於 MapControllers 完成後，觸發 Dynamic Action 異動通知刷洗 Endpoint 數據源
    var changeProvider = app.Services.GetRequiredService<IDynamicActionDescriptorChangeProvider>(); 
    changeProvider.NotifyChanges();
    // ====================================================
    // 雙 SPA (Blazor WASM / Vue 3) 路由分流
    // ====================================================
    // 使用 SpaFallbackExtensions 提供的擴充方法掛載 Blazor WASM (精準匹配 /blazor, /blazor/, /blazor/xxx)
    app.MapBlazorSpaFallback();

    // Vue 3 專屬後備管道 (非 API/Scalar/OpenAPI/Blazor 之請求全數歸 Vue 3 接管)[cite: 53]
    app.MapProtectedSpaFallback(app.Environment, "index.html");

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