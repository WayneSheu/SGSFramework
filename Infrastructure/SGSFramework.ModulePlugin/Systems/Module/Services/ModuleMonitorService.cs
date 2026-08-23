using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Alerts;
using SGSFramework.ModulePlugin.Abstractions;

namespace SGSFramework.ModulePlugin.Systems.Module.Services;

/// <summary>
/// 模組監控服務。
/// 背景監控服務，定期將模組狀態輸出至日誌，並具備防洪機制抑制重複告警。
/// </summary>
public class ModuleMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ModuleMonitorService> _logger;
    private readonly IAlertFloodSuppressor _floodSuppressor;

    public ModuleMonitorService(
        IServiceScopeFactory scopeFactory,
        ILogger<ModuleMonitorService> logger,
        IAlertFloodSuppressor floodSuppressor)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _floodSuppressor = floodSuppressor ?? throw new ArgumentNullException(nameof(floodSuppressor));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var registry = scope.ServiceProvider.GetRequiredService<IModuleRegistry>();
                var reports = registry.GetAllStatuses();

                foreach (var report in reports)
                {
                    if (!report.IsDependenciesSatisfied)
                    {
                        var alertKey = $"ModuleDepUnsatisfied:{report.ModuleName}";
                        
                        // 檢查是否觸發防洪機制 (10 分鐘內不重複分發)
                        bool isSuppressed = await _floodSuppressor.ShouldSuppressAsync(alertKey, TimeSpan.FromMinutes(10), stoppingToken);

                        if (!isSuppressed)
                        {
                            _logger.LogError("[ALARM FLOOD CLEAR] 模組相依性異常告警分發：Module: {Name}, LoadTime: {Time}ms",
                                report.ModuleName,
                                report.LoadDuration.TotalMilliseconds);
                        }
                        else
                        {
                            _logger.LogWarning("[ALARM SUPPRESSED] 抑制重複告警分發：Module: {Name}", report.ModuleName);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("Module: {Name}, LoadTime: {Time}ms, Memory: {Mem}bytes, DepsOk: {Deps}",
                            report.ModuleName,
                            report.LoadDuration.TotalMilliseconds,
                            report.MemoryUsageBytes,
                            report.IsDependenciesSatisfied);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行模組監控時發生未預期錯誤。");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}