using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using SGSFramework.AuditLog.Extensions;
using SGSFramework.Core.Migrations;
using SGSFramework.ModulePlugin.Abstractions;
using SGSFramework.ModulePlugin.Systems.Controller.Providers;
using SGS.Modules.ORG.Infrastructure;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGS.Modules.ORG.Infrastructure.Extensions;
using SGS.Modules.ORG.Application.Extensions;
using SGS.Modules.ORG.Application.Services;
using SGS.Modules.ORG.Extensions;
using System.Collections.Concurrent;

namespace SGS.Modules.ORG.Initializer;

public class ModuleOrgInitializer : IModuleInitializer
{
    private static readonly ConcurrentDictionary<string, bool> MigratedModules = new();
    private IServiceProvider? _serviceProvider;
    private bool _isInitialized;

    public string ModuleName => "SGS.Modules.ORG";

    // 定義核心依賴檢查清單
    private readonly Type[] _requiredServices =
    [
        typeof(ORGDbContext),
        typeof(IMediator),
        typeof(OrganizationService)
    ];

    public ModuleHealthReport GetHealthStatus()
    {
        if (_serviceProvider is null)
        {
            return new ModuleHealthReport(ModuleName, TimeSpan.Zero, false, GC.GetTotalMemory(false));
        }

        bool isHealthy = false;
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var provider = scope.ServiceProvider;

            var db = provider.GetService<ORGDbContext>();
            bool dbConnected = db != null && db.Database.CanConnect();
            bool dependenciesSatisfied = dbConnected && CheckDependencies(provider);

            isHealthy = dependenciesSatisfied;
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

    /// <summary>
    /// 註冊模組的依賴服務
    /// </summary>
    public IServiceCollection RegisterDependencies(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            Log.Information(">>> 正在註冊模組: {ModuleName} 的依賴項目", ModuleName);

            // 1. 註冊 OrganizationService 實體與介面，確保 CheckDependencies 驗證通過
            services.TryAddScoped<OrganizationService>();

            // 2. AddModuleDatabaseWithAudit 統一配置 DbContextFactory, StorageStrategy 與 AuditWorker
            services.AddModuleDatabaseWithAudit<ORGDbContext>(configuration, "PersistentOptions:DatabaseSettings:ConnectionString");

            // 3. 註冊該模組專屬的遷移服務與內部層級服務
            services.AddScoped<IMigrationService, OrgMigrationService>();
            services.AddModuleOrg(configuration);
            services.AddModuleOrgApplication(configuration);
            services.AddModuleORGInfrastructure();

            // 4. 註冊動態控制器發現器
            services.AddControllers()
                    .ConfigureApplicationPartManager(manager =>
                    {
                        manager.FeatureProviders.Add(new InternalControllerFeatureProvider());
                    });

            return services;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "模組 {ModuleName} 依賴註冊失敗。", ModuleName);
            throw new InvalidOperationException($"模組:{ModuleName} 初始化失敗，原因:{ex.Message}", ex);
        }
    }

    /// <summary>
    /// 在應用程式啟動時執行資料庫遷移
    /// </summary>
    public async Task OnApplicationConfigureAsync(IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        _serviceProvider = app.ApplicationServices;

        if (!MigratedModules.TryAdd(ModuleName, true))
        {
            Log.Debug("[{ModuleName}] 模組資料庫遷移已於先前完成，跳過重複執行。", ModuleName);
            return;
        }

        using var scope = app.ApplicationServices.CreateScope();

        try
        {
            var dbContext = scope.ServiceProvider.GetService<ORGDbContext>();
            if (dbContext is null)
            {
                throw new InvalidOperationException($"無法自 DI 容器取得 {ModuleName} 之 ORGDbContext 實例。");
            }

            Log.Information("開始執行 {ModuleName} 模組資料庫自動遷移 (Schema: org)...", ModuleName);

            await dbContext.Database.MigrateAsync();

            _isInitialized = true;
            Log.Information("{ModuleName} 模組資料庫遷移作業成功完成。", ModuleName);
        }
        catch (Exception ex)
        {
            MigratedModules.TryRemove(ModuleName, out _);

            Log.Fatal(ex, "{ModuleName} 資料庫遷移作業失敗！", ModuleName);
            throw;
        }
    }
}