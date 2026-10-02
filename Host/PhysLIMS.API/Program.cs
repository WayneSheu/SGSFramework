// Path: src/SGSFramework/Host/PhysLIMS.API/Program.cs
using MediatR;
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
using SGSFramework.SystemLog.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // 1. 初始化系統日誌與核心框架
    builder.AddSystemLog();
    Log.Information("Starting WebAPI Application.");

    IConfiguration config = builder.Configuration;
    builder.AddSGSFrameworkCore();

    // 2. 註冊多版本 API 與文件 (v1, v2)
    builder.Services.AddEnterpriseApiVersioningAndDocs();

    // 3. 註冊資料庫基礎設施與 PhysLIMSDbContext 組合
    builder.Services.AddCoreDatabaseInfrastructure(config);

    // 4. 控制器與動態外掛模組註冊
    builder.Services.AddControllerInfrastructure(config);
    builder.Services.AddCustomApiBehavior();
    builder.Services.AddModulePlugin<PhysLIMSDbContext>(config);
    builder.Services.AddControllerScanner<PhysLIMSDbContext>();

    // 5. 跨域、例外處理與安全身分驗證
    builder.Services.AddEnterpriseCorsPolicy(config);
    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddTokenBucketSecurity(config);

    // 6. 註冊 MediatR 服務與 CQRS Handlers 掃描
    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(GetFullMenuTreeQueryHandler).Assembly);
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

    // 7. 資安防護、帳本驗證與生產管理者 Seed 服務 (收納於 WebApplicationBuilderExtensions)
    builder.Services.AddEnterpriseSecurityAndSeeders(config);
    builder.AddDIContainerValidation();

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

    // ----------------------------------------------------
    // 中間件管道配置 (Middleware Pipeline Execution Order)
    // ----------------------------------------------------
    app.UseExceptionHandler();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }
    else
    {
        app.UseDeveloperExceptionPage();
    }

    // 9. 靜態檔案與 Blazor WASM / Vue 資源託管
    app.UseEnterpriseStaticFiles();

    // 10. 資料庫自動 Migration 與種子資料同步
    await app.ExecuteStartupSeedersAsync(config).ConfigureAwait(false);

    // 11. 動態外掛模組初始化
    await app.InitializeModularSystemAsync().ConfigureAwait(false);
    await app.UseDynamicControllersAsync().ConfigureAwait(false);

    // 12. 跨域與安全中間件
    app.UseCors("CorsPolicy");
    app.UseMiddleware<CorsLoggingMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();

    // 13. OpenAPI 快取控制標頭設定
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

    // 14. 映射 Endpoints (OpenAPI / Scalar / API Controllers)
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "PhysLIMS 2.0 API";
        options.Theme = ScalarTheme.Solarized;
        options.Layout = ScalarLayout.Modern;
        options.Authentication = new ScalarAuthenticationOptions
        {
            PreferredSecurityScheme = JwtBearerDefaults.AuthenticationScheme
        };
    });

    app.MapControllers();

    // 刷新 Dynamic Action 變更通知
    var changeProvider = app.Services.GetRequiredService<IDynamicActionDescriptorChangeProvider>();
    changeProvider.NotifyChanges();

    // 15. 多 SPA Fallback 隔離路由配置
    app.MapWhen(context =>
        !context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) &&
        !context.Request.Path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase) &&
        !context.Request.Path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase) &&
        !context.Request.Path.StartsWithSegments("/blazor", StringComparison.OrdinalIgnoreCase),
        builderApp =>
        {
            builderApp.UseRouting();
            builderApp.UseEndpoints(endpoints =>
            {
                endpoints.MapProtectedSpaFallback(app.Environment, "index.html");
            });
        });

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