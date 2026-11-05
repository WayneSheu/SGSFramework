using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SGS.Modules.LOG.Application.Extensions;
using SGS.Modules.LOG.Infrastructure.Dbcontexts;
using SGS.Modules.LOG.Infrastructure.Extensions;
using SGS.Modules.LOG.Infrastructure.Services;
using SGSFramework.Core.Logging;
using SGSFramework.ModulePlugin.Abstractions;

namespace SGS.Modules.LOG.Initializer;

/// <summary>
/// LOG 日誌管理外掛模組初始化器：註冊獨立日誌庫 (LogDbContext) 與三種日誌的儲存實作（覆蓋 Core Null 緩衝 store）。
/// 未設定 PersistentSettings:ConnectionStrings:LogConnection 時整個模組停用，Host 維持原行為（不註冊、不遷移）。
/// 啟動時先以 LogDatabaseProvisioner 自動建立獨立日誌庫（不存在時）與帳號權限，再套用 Migration。
/// 日誌庫連不上時不阻擋啟動：Host Worker 會把日誌落檔至 Log_Buffer，恢復後自動回補。
/// 模組含 Singleton store，不支援執行中熱抽換，更新模組須重新啟動 API。
/// </summary>
public sealed class ModuleLogInitializer : IModuleInitializer
{
    private static readonly SemaphoreSlim MigrationLock = new(1, 1);
    private static volatile bool _isMigrated;

    private IServiceProvider? _serviceProvider;
    private bool _enabled;

    public string ModuleName => "SGS.Modules.LOG";

    public ModuleHealthReport GetHealthStatus()
    {
        // 未設定 LogConnection 屬刻意停用（沿用 Host 內建寫入），不應觸發「模組相依性異常」告警
        if (!_enabled)
        {
            return new ModuleHealthReport(ModuleName, TimeSpan.Zero, true, GC.GetTotalMemory(false));
        }

        if (_serviceProvider is null)
        {
            return new ModuleHealthReport(ModuleName, TimeSpan.Zero, false, GC.GetTotalMemory(false));
        }

        bool healthy;
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetService<LogDbContext>();
            healthy = _isMigrated && db is not null && db.Database.CanConnect();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[{ModuleName}] 健康檢查執行期間發生例外異常。", ModuleName);
            healthy = false;
        }

        return new ModuleHealthReport(ModuleName, TimeSpan.Zero, healthy, GC.GetTotalMemory(false));
    }

    public IServiceCollection RegisterDependencies(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!LogDatabaseOptions.IsConfigured(configuration))
        {
            Log.Warning("[{ModuleName}] 未設定 {Key}，日誌模組停用（沿用 Host 內建日誌寫入）。", ModuleName, LogDatabaseOptions.RuntimeConnectionKey);
            return services;
        }

        try
        {
            Log.Information(">>> 正在註冊模組: {ModuleName} 的依賴項目", ModuleName);

            services.AddModuleLogInfrastructure(configuration);
            services.AddModuleLogApplication();
            _enabled = true;

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

        if (!_enabled || _isMigrated) return;

        await MigrationLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_isMigrated) return;

            using var scope = app.ApplicationServices.CreateScope();

            // 部署 / 上傳模組後首次啟動：日誌庫不存在時自動建立，並補齊執行期 / 備份帳號的使用者與權限
            await scope.ServiceProvider.GetRequiredService<LogDatabaseProvisioner>().EnsureProvisionedAsync().ConfigureAwait(false);

            var migrationService = scope.ServiceProvider.GetRequiredService<LogMigrationService>();

            Log.Information("開始執行 {ModuleName} 模組資料庫自動遷移 (Schema: log)...", ModuleName);
            await migrationService.MigrateAsync().ConfigureAwait(false);

            _isMigrated = true;
            Log.Information("{ModuleName} 模組資料庫遷移作業成功完成。", ModuleName);
        }
        catch (Exception ex)
        {
            // 日誌庫不可用不得阻擋 API 啟動：寫入失敗由 Host Worker 落檔緩衝，DB 恢復並重啟後回補
            Log.Error(ex, "{ModuleName} 資料庫遷移失敗，日誌將暫存於 Log_Buffer 待資料庫恢復後回補。", ModuleName);
        }
        finally
        {
            MigrationLock.Release();
        }
    }
}
