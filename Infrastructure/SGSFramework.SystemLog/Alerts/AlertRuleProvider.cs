#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Entities.Alerts;

namespace SGSFramework.SystemLog.Alerts
{
    /// <summary>告警規則提供者：從資料庫載入啟用規則並熱更新到 <see cref="AlertRuleStore"/></summary>
    public interface IAlertRuleProvider
    {
        AlertRuleSnapshot Current { get; }

        /// <summary>重新載入規則（規則異動後立即呼叫；其他執行個體由背景服務定期重載）</summary>
        Task ReloadAsync(CancellationToken cancellationToken = default);
    }

    public sealed class AlertRuleProvider : IAlertRuleProvider
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AlertRuleProvider> _logger;

        public AlertRuleProvider(IServiceScopeFactory scopeFactory, ILogger<AlertRuleProvider> logger)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public AlertRuleSnapshot Current => AlertRuleStore.Current;

        public async Task ReloadAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbContext>();

            var rules = await context.Set<AlertRule>()
                .AsNoTracking()
                .Where(r => r.IsEnabled)
                .OrderBy(r => r.Priority)
                .ThenBy(r => r.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            AlertRuleStore.Replace(new AlertRuleSnapshot(rules));
            _logger.LogDebug("告警規則已重新載入，啟用 {Count} 條。", rules.Count);
        }
    }

    /// <summary>
    /// 啟動時於資料庫沒有任何規則時寫入內建預設規則，並定期重載規則（多個執行個體或直接改資料庫時也能生效）。
    /// 資料表尚未建立（Migration 未套用）或暫時連不上資料庫時，沿用內建預設規則，告警不中斷。
    /// </summary>
    public sealed class AlertRuleRefreshService : BackgroundService
    {
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);

        private readonly IAlertRuleProvider _provider;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AlertRuleRefreshService> _logger;
        private bool _seeded;
        private bool _failureLogged;

        public AlertRuleRefreshService(
            IAlertRuleProvider provider,
            IServiceScopeFactory scopeFactory,
            ILogger<AlertRuleRefreshService> logger)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(RefreshInterval);

            do
            {
                try
                {
                    if (!_seeded) await SeedDefaultsAsync(stoppingToken).ConfigureAwait(false);
                    await _provider.ReloadAsync(stoppingToken).ConfigureAwait(false);
                    _failureLogged = false;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // 只在狀態改變時記一次，避免資料表未建立時每分鐘洗版
                    if (!_failureLogged)
                    {
                        _logger.LogWarning(ex, "載入告警規則失敗，沿用目前（預設）規則。請確認已套用 AlertRules 的資料庫 Migration。");
                        _failureLogged = true;
                    }
                }
            }
            while (await WaitAsync(timer, stoppingToken).ConfigureAwait(false));
        }

        private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken cancellationToken)
        {
            try
            {
                return await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private async Task SeedDefaultsAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<DbContext>();
            var set = context.Set<AlertRule>();

            if (!await set.AnyAsync(cancellationToken).ConfigureAwait(false))
            {
                set.AddRange(AlertRuleStore.DefaultRules());
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("資料庫尚無告警規則，已寫入內建預設規則。");
            }

            _seeded = true;
        }
    }
}
