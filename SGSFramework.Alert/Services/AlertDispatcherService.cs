using Microsoft.Extensions.Logging;
using SGSFramework.Alert.Abstractions;
using SGSFramework.Core.Alerts;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Alert.Services
{
    /// <summary>
    /// 整合防洪抑制與策略模式與 Fallback 機制的告警調度服務
    /// </summary>
    public sealed class AlertDispatcherService : IAlertDispatcherService
    {
        private readonly IEnumerable<IAlertNotificationChannel> _channels;
        private readonly IAlertFloodSuppressor _floodSuppressor;
        private readonly ILogger<AlertDispatcherService> _logger;

        // 預設防洪冷卻時間 (例如：同特徵告警 5 分鐘內不重複發送)
        private static readonly TimeSpan DefaultCooldown = TimeSpan.FromMinutes(5);

        public AlertDispatcherService(
            IEnumerable<IAlertNotificationChannel> channels,
            IAlertFloodSuppressor floodSuppressor,
            ILogger<AlertDispatcherService> logger)
        {
            _channels = channels ?? throw new ArgumentNullException(nameof(channels));
            _floodSuppressor = floodSuppressor ?? throw new ArgumentNullException(nameof(floodSuppressor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task DispatchAsync(AlertMessageModel alert, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(alert);

            string fingerprint = alert.GetFingerprintKey();

            // 1. 執行防洪檢查 (IAlertFloodSuppressor)
            bool isSuppressed = await _floodSuppressor.ShouldSuppressAsync(fingerprint, DefaultCooldown, cancellationToken);
            if (isSuppressed)
            {
                _logger.LogInformation("[Alerts.Core] 告警觸發防洪機制，已自動抑制發送。Fingerprint: {Fingerprint}, AlertId: {AlertId}",
                    fingerprint, alert.AlertId);
                return;
            }

            // 2. 篩選可用管道
            var activeChannels = _channels.Where(c => c.IsConfigured()).ToList();
            if (!activeChannels.Any())
            {
                _logger.LogWarning("[Alerts.Core] 無任何有效組態之告警管道。AlertId: {AlertId}", alert.AlertId);
                return;
            }

            // 3. 併行或依序發送至各管道策略 (IAlertNotificationChannel)
            bool anySuccess = false;
            foreach (var channel in activeChannels)
            {
                try
                {
                    bool result = await channel.SendAlertAsync(alert, cancellationToken);
                    if (result)
                    {
                        anySuccess = true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Alerts.Core] 管道 [{ChannelName}] 發送告警異常。AlertId: {AlertId}", channel.ChannelName, alert.AlertId);
                }
            }

            if (!anySuccess)
            {
                _logger.LogError("[Alerts.Core] 所有告警管道執行失敗。AlertId: {AlertId}", alert.AlertId);
            }
        }
    }
}
