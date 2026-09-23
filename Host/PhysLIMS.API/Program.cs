// Path: src/SGSFramework/Host/PhysLIMS.API/Program.cs
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using PhysLIMS.API.Dbcontexts;
using Scalar.AspNetCore;
using Serilog;
using SGSFramework.ApiInfrastructure.Bootstrappers;
using SGSFramework.ApiInfrastructure.DependencyInjection;
using SGSFramework.ApiInfrastructure.Filters;
using SGSFramework.ApiInfrastructure.Middlewares;
using SGSFramework.ApiInfrastructure.Transformers;
using SGSFramework.AuditLog.Extensions;
using SGSFramework.AuthTokenBucket.Abstractions;
using SGSFramework.AuthTokenBucket.Extensions;
using SGSFramework.AuthTokenBucket.Queries.Menuitems;
using SGSFramework.CodeSecurity.Extensions;
using SGSFramework.Core.Abstractions.Database;
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
using SGSFramework.Persistent.Extensions;
using SGSFramework.Persistent.ScriptRunners;
using SGSFramework.SystemLog.Extensions;
using SGSFramework.VerifyLedger.Extensions;
using System.Reflection;

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.AddSystemLog();
    Log.Information("Starting WebAPI Application.");

    IConfiguration config = builder.Configuration;
    builder.AddSGSFrameworkCore();

    // 1. OpenAPI 與 Scalar 文件設定
    builder.Services.AddOpenApi("v1", options =>
    {
        options.ShouldInclude = (description) => true;
        options.AddOperationTransformer<MenuAttributeTransformer>();
        options.AddDocumentTransformer<DynamicControllerDocumentFilter>();
        options.AddDocumentTransformer<OpenApiSecurityRequirementTransformer>();
    });

    builder.Services.AddAPIDocServices();

    // 2. 註冊 AuditLog 基礎設施 (包含 Options, HttpContextAccessor, IAuditProvider, Interceptors)
    builder.Services.AddAuditLog(config);

    // 3. 資料庫基礎設施與 PhysLIMSDbContext 專屬 Audit/Channel/Worker 組合註冊
    builder.Services.AddPersistentServices();

    builder.Services.AddModuleDatabaseWithAudit<PhysLIMSDbContext>(
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

    builder.Services.AddScoped<ICoreDbContext>(sp => sp.GetRequiredService<PhysLIMSDbContext>());
    builder.Services.AddScoped<ITokenDbContext>(sp => sp.GetRequiredService<PhysLIMSDbContext>());
    builder.Services.AddScoped<DbContext>(sp => sp.GetRequiredService<PhysLIMSDbContext>());

    // 4. ASP.NET Core Identity 打包註冊
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

    // 5. 控制器與動態外掛模組註冊
    builder.Services.AddControllerInfrastructure(config);
    builder.Services.AddCustomApiBehavior();
    builder.Services.AddModulePlugin<PhysLIMSDbContext>(config);
    builder.Services.AddControllerScanner<PhysLIMSDbContext>();

    // 6. 企業級 CORS 策略註冊
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("CorsPolicy", policy =>
        {
            var allowedOrigins = builder.Configuration
                .GetSection("CorsSettings:AllowedOrigins")
                .Get<string[]>();

            // Fail-Fast: 無論哪個環境，只要未設定有效網域即中斷啟動
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

    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // 7. Token Bucket 身份驗證與授權配置
    var scannedAssemblies = AppDomain.CurrentDomain.GetAssemblies()
        .Where(a => a.FullName != null &&
                   (a.FullName.StartsWith("SGS.") ||
                    a.FullName.StartsWith("PhysLIMS") ||
                    a == Assembly.GetEntryAssembly()))
        .Distinct()
        .ToArray();

    builder.Services.AddTokenBucketAuthentication<PhysLIMSDbContext, ApplicationUser>(options =>
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

    // 8. 註冊 MediatR 服務與 CQRS Handlers 掃描
    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssembly(typeof(GetFullMenuTreeQueryHandler).Assembly);
    });

    builder.Services.AddSSOServices();
    builder.Services.AddAuthorization();

    #region 身分驗證環境配置 (IIS / Kestrel)
    if (!builder.Environment.IsDevelopment())
    {
        var isIISHosted = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APP_POOL_ID")) ||
                          !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANCM_PREFER_USER_STORE"));

        if (isIISHosted)
        {
            Log.Information("偵測到 IIS 託管環境，整合 IIS Native Windows Authentication。");
            RemoveNegotiateServices(builder.Services);
        }
    }
    #endregion

    builder.Services.AddProductionAdminSeeder(builder.Configuration);
    builder.Services.AddLedgerVerificationServices();
    builder.Services.AddCodeSecurity(config);
    builder.AddDIContainerValidation();

    var app = builder.Build();

    var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
    var bootstrapLogger = loggerFactory.CreateLogger("IisBootstrapExecution");

    try
    {
        IisBootstrapTask.Execute(builder.Configuration, bootstrapLogger);
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

    // 資料庫自動 Migration 與腳本初始化流程 (已修正：改由 DI 容器解析完整具備 Interceptor 的 DbContext)
    var autoMigrate = config.GetValue<bool>("Database:AutoMigrate", true);
    if (app.Environment.IsDevelopment() || autoMigrate)
    {
        using var scope = app.Services.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();
        await initializer.InitializeDatabaseAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);

        var mainDbContext = scope.ServiceProvider.GetRequiredService<PhysLIMSDbContext>();
        await mainDbContext.Database.MigrateAsync(app.Lifetime.ApplicationStopping).ConfigureAwait(false);
    }

    // 執行模組載入初始化動態控制器與權限、Menu 種子同步
    await app.InitializeModularSystemAsync().ConfigureAwait(false);
    await app.UseDynamicControllersAsync().ConfigureAwait(false);

    // 執行動態權限與選單樹狀結構種子同步
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;

        // 同步權限點與 BitPosition
        var permissionSeeder = services.GetRequiredService<IPermissionSeedService>();
        await permissionSeeder.SeedAndSyncPermissionsAsync().ConfigureAwait(false);

        // 同步選單樹 (Section -> Group -> Page)
        var menuSeeder = services.GetRequiredService<IMenuSeedService>();
        await menuSeeder.SeedAndSyncMenusAsync().ConfigureAwait(false);
    }

    // 路由與跨域中間件
    app.UseRouting();
    app.UseMiddleware<CorsLoggingMiddleware>();
    app.UseCors("CorsPolicy");

    // 驗證與授權中間件
    app.UseAuthentication();
    app.UseAuthorization();

    var changeProvider = app.Services.GetRequiredService<IDynamicActionDescriptorChangeProvider>();
    changeProvider.NotifyChanges();

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
    app.MapGet("/", context =>
    {
        context.Response.Redirect("/scalar/v1");
        return Task.CompletedTask;
    });

    await app.RunAsync().ConfigureAwait(false);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
    Environment.ExitCode = 1;
    throw;
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
    }
}