using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SGS.Modules.MAN.Application.Extensions;
using SGS.Modules.MAN.Application.Services;
using SGS.Modules.MAN.Infrastructure.Dbcontexts;
using SGS.Modules.MAN.Infrastructure.Extensions;
using SGS.Modules.MAN.Infrastructure.Services;
using SGSFramework.Core.Controllers.Providers;
using SGSFramework.ModulePlugin.Abstractions;

namespace SGS.Modules.MAN.Initializer;

/// <summary>
/// MAN 操作手冊管理外掛模組初始化器：註冊 DI 並於啟動時套用 man schema 遷移。
/// </summary>
public sealed class ModuleManInitializer : IModuleInitializer
{
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static volatile bool _isMigrated;

    private IServiceProvider? _serviceProvider;
    private bool _isInitialized;

    public string ModuleName => "SGS.Modules.MAN";

    private readonly Type[] _requiredServices =
    [
        typeof(MANDbContext),
        typeof(ManualTopicService),
        typeof(ManMigrationService)
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

            var dbContext = provider.GetService<MANDbContext>();
            bool dbConnected = dbContext is not null && dbContext.Database.CanConnect();
            isHealthy = dbConnected && CheckDependencies(provider) && _isInitialized;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[{ModuleName}] 健康檢查執行期間發生例外異常。", ModuleName);
            isHealthy = false;
        }

        return new ModuleHealthReport(ModuleName, TimeSpan.Zero, isHealthy, GC.GetTotalMemory(false));
    }

    public IServiceCollection RegisterDependencies(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        try
        {
            Log.Information(">>> 正在註冊模組: {ModuleName} 的依賴項目", ModuleName);

            services.AddModuleManInfrastructure(configuration);
            services.AddModuleManApplication(configuration);

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
            if (_isMigrated) return;

            using var scope = app.ApplicationServices.CreateScope();
            var migrationService = scope.ServiceProvider.GetService<ManMigrationService>()
                ?? throw new InvalidOperationException($"無法自 DI 容器取得 {ModuleName} 之 ManMigrationService 實例。");

            Log.Information("開始執行 {ModuleName} 模組資料庫自動遷移 (Schema: man)...", ModuleName);
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
