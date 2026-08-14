using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using SGS.Modules.ORG.Application;
using SGS.Modules.ORG.Application.Services;
using SGS.Modules.ORG.Infrastructure;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGSFramework.AuditLog.Extensions;
using SGSFramework.Core.Abstractions.Adapters;
using SGSFramework.Core.Controllers.Providers;
using SGSFramework.Core.Migrations;
using SGSFramework.ModulePlugin.Abstractions;
using SGSFramework.Persistent.Repositories.Hierarchy;

namespace SGS.Modules.ORG.Initializer;

public sealed class ModuleOrgInitializer : IModuleInitializer
{
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static volatile bool _isMigrated;

    private IServiceProvider? _serviceProvider;
    private bool _isInitialized;

    public string ModuleName => "SGS.Modules.ORG";

    private readonly Type[] _requiredServices =
    [
        typeof(ORGDbContext),
        typeof(IMediator),
        typeof(IOrganizationService),
        typeof(IMigrationService)
    ];

    public ModuleHealthReport GetHealthStatus()
    {
        if (_serviceProvider is null)
        {
            return new ModuleHealthReport(ModuleName, TimeSpan.Zero, false, GC.GetTotalMemory(false));
        }

        bool isHealthy;
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var provider = scope.ServiceProvider;

            var dbContext = provider.GetService<ORGDbContext>();
            bool dbConnected = dbContext is not null && dbContext.Database.CanConnect();
            bool dependenciesSatisfied = dbConnected && CheckDependencies(provider);

            isHealthy = dependenciesSatisfied && _isInitialized;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[{ModuleName}] 健康檢查執行期間發生例外異常。", ModuleName);
            isHealthy = false;
        }

        return new ModuleHealthReport(
            ModuleName,
            TimeSpan.Zero,
            isHealthy,
            GC.GetTotalMemory(false)
        );
    }

    public IServiceCollection RegisterDependencies(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            Log.Information(">>> 正在註冊模組: {ModuleName} 的依賴項目", ModuleName);

            // 1. 補齊 MediatR 模組管線註冊 (確保 IMediator 服務可用)
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(ModuleOrgInitializer).Assembly);
                cfg.RegisterServicesFromAssembly(typeof(ORGDbContext).Assembly);
            });

            // 2. 註冊 Hierarchy Repository
            services.TryAddScoped(
                typeof(IHierarchicalRepository<ORGDbContext, Organization>),
                typeof(HierarchicalRepository<ORGDbContext, Organization>));

            // 3. 註冊 Organization 業務介面與實作
            services.TryAddScoped<IOrganizationService, OrganizationService>();
            services.TryAddScoped<OrganizationService>();
            services.AddScoped<IOrganizationIntegrationService, OrganizationIntegrationService>();

            // 4. 註冊資料庫與指定正確 Migration Assembly (指向 Infrastructure 層)
            services.AddModuleDatabaseWithAudit<ORGDbContext>(
                configuration,
                "PersistentSettings:ConnectionStrings",
                "org",
                dbContextOptionsBuilder =>
                {
                    var infrastructureAssembly = typeof(ORGDbContext).Assembly;
                    dbContextOptionsBuilder.UseSqlServer(sqlOptions =>
                    {
                        sqlOptions.MigrationsAssembly(infrastructureAssembly.FullName);
                        sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "org");
                    });
                });

            // 5. 註冊基礎設施與遷移服務
            services.AddScoped<IMigrationService, OrgMigrationService>();

            // 6. 配置 MVC Controller 掃描提供者
            services.AddControllers()
                    .ConfigureApplicationPartManager(manager =>
                    {
                        manager.FeatureProviders.Add(new InternalControllerFeatureProvider());
                    });

            Log.Information("模組 {ModuleName} 依賴註冊完成。", ModuleName);
            return services;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "模組 {ModuleName} 依賴註冊失敗。", ModuleName);
            throw new InvalidOperationException($"模組:{ModuleName} 初始化失敗，原因:{ex.Message}", ex);
        }
    }

    public async Task OnApplicationConfigureAsync(IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _serviceProvider = app.ApplicationServices;

        if (_isMigrated)
        {
            Log.Debug("[{ModuleName}] 模組資料庫遷移已於先前完成，跳過重複執行。", ModuleName);
            return;
        }

        await MigrationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isMigrated)
            {
                Log.Debug("[{ModuleName}] 模組資料庫遷移已於先前完成，跳過重複執行。", ModuleName);
                return;
            }

            using var scope = app.ApplicationServices.CreateScope();
            var migrationService = scope.ServiceProvider.GetService<IMigrationService>();

            if (migrationService is null)
            {
                throw new InvalidOperationException($"無法自 DI 容器取得 {ModuleName} 之 IMigrationService 實例。");
            }

            Log.Information("開始執行 {ModuleName} 模組資料庫自動遷移 (Schema: org)...", ModuleName);

            await migrationService.MigrateAsync().ConfigureAwait(false);

            _isMigrated = true;
            _isInitialized = true;
            Log.Information("{ModuleName} 模組資料庫遷移作業成功完成。", ModuleName);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "{ModuleName} 資料庫遷移作業失敗！", ModuleName);
            throw;
        }
        finally
        {
            MigrationLock.Release();
        }
    }

    private bool CheckDependencies(IServiceProvider provider)
    {
        foreach (var serviceType in _requiredServices)
        {
            if (provider.GetService(serviceType) is null)
            {
                Log.Warning("[{ModuleName}] 缺少必要依賴服務: {ServiceType}", ModuleName, serviceType.Name);
                return false;
            }
        }
        return true;
    }
}