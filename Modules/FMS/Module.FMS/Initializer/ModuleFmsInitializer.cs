using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SGS.Modules.FMS.Application.Extensions;
using SGS.Modules.FMS.Infrastructure.Dbcontexts;
using SGS.Modules.FMS.Infrastructure.Extensions;
using SGS.Modules.FMS.Infrastructure.Services;
using SGSFramework.Core.FileManagement;
using SGSFramework.Core.Controllers.Providers;
using SGSFramework.ModulePlugin.Abstractions;

namespace SGS.Modules.FMS.Initializer;

/// <summary>
/// FMS 檔案管理外掛模組初始化器：註冊 DI（覆蓋 Core 的 NullFileManagementService）並於啟動時套用 fms schema 遷移。
/// </summary>
public sealed class ModuleFmsInitializer : IModuleInitializer
{
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static volatile bool _isMigrated;

    private IServiceProvider? _serviceProvider;
    private bool _isInitialized;

    public string ModuleName => "SGS.Modules.FMS";

    private readonly Type[] _requiredServices =
    [
        typeof(FMSDbContext),
        typeof(IFileManagementService),
        typeof(FmsMigrationService)
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

            var dbContext = provider.GetService<FMSDbContext>();
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

            // 1. Infrastructure 層 DI 擴充 (FMSDbContext、Migration Service、組態與儲存體工廠)
            services.AddModuleFmsInfrastructure(configuration);

            // 2. Application 層 DI 擴充 (檔案管理服務、簽章檢查、影像處理與縮圖快取)
            services.AddModuleFmsApplication(configuration);

            // 3. 配置 Dynamic Controller 特性提供者
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
            var migrationService = scope.ServiceProvider.GetService<FmsMigrationService>();

            if (migrationService is null)
            {
                throw new InvalidOperationException($"無法自 DI 容器取得 {ModuleName} 之 FmsMigrationService 實例。");
            }

            Log.Information("開始執行 {ModuleName} 模組資料庫自動遷移 (Schema: fms)...", ModuleName);

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