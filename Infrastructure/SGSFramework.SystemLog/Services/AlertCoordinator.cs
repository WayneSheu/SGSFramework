#nullable enable
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Notifications;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.SystemLog.Services
{
    /// <summary>
    /// 告警調度中心實作（採用策略模式管理多路通知）
    /// </summary>
    public sealed class AlertCoordinator : IAlertCoordinator
    {
        private readonly IEnumerable<INotificationStrategy> _strategies;
        private readonly ILogger<AlertCoordinator> _logger;

        public AlertCoordinator(
            IEnumerable<INotificationStrategy> strategies,
            ILogger<AlertCoordinator> logger)
        {
            _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task DispatchAsync(AlertMessage message, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message);

            foreach (var strategy in _strategies)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    _logger.LogInformation("開始透過管道 [{Channel}] 發送告警。AlertId: {AlertId}", strategy.ChannelName, message.AlertId);
                    await strategy.SendAlertAsync(message, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // 確保單一管道失敗（如 SMTP 斷線）不會阻斷其他管道（如 Teams）的發送
                    _logger.LogError(ex, "通知管道 [{Channel}] 執行失敗。AlertId: {AlertId}", strategy.ChannelName, message.AlertId);
                }
            }
        }
    }
}