using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using SGSFramework.ModulePlugin.Abstractions;

namespace SGSFramework.ModulePlugin.Systems.Module.Services
{
    /// <summary>
    /// 模組監控服務。
    /// 背景監控服務，定期將模組狀態輸出至日誌。
    /// 使用 IServiceScopeFactory 解決 Singleton 依賴 Scoped 服務的問題。
    /// </summary>
    public class ModuleMonitorService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ModuleMonitorService> _logger;

        public ModuleMonitorService(IServiceScopeFactory scopeFactory, ILogger<ModuleMonitorService> _logger)
        {
            _scopeFactory = scopeFactory;
            this._logger = _logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 手動建立 Scope 以解析 Scoped 的 IModuleRegistry
                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var registry = scope.ServiceProvider.GetRequiredService<IModuleRegistry>();
                        var reports = registry.GetAllStatuses();

                        foreach (var report in reports)
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
                    _logger.LogError(ex, "執行模組監控時發生錯誤。");
                }

                // 等待 5 分鐘後進行下一次監控
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
    }
}