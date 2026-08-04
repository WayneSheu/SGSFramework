using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Notifications;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;

namespace SGSFramework.SystemLog.BackgroundServices
{
    /// <summary>
    /// 長駐背景服務：負責消費告警管道並透過多路策略(Strategy Pattern)進行通知分發
    /// </summary>
    public sealed class AlertNotificationWorker : BackgroundService
    {
        private readonly ChannelReader<AlertMessage> _channelReader;
        private readonly IEnumerable<INotificationStrategy> _strategies;
        private readonly ILogger<AlertNotificationWorker> _logger;

        public AlertNotificationWorker(
            Channel<AlertMessage> alertChannel,
            IEnumerable<INotificationStrategy> strategies,
            ILogger<AlertNotificationWorker> logger)
        {
            ArgumentNullException.ThrowIfNull(alertChannel);
            _channelReader = alertChannel.Reader;
            _strategies = strategies ?? throw new ArgumentNullException(nameof(strategies));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AlertNotificationWorker 已成功啟動，開始監聽告警管道...");

            // 使用 C# 10+ 強型別非同步串流持續讀取管道資料
            await foreach (var message in _channelReader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    // 核心調度：並行或依序觸發所有註冊的通知策略 (例如：Email, Teams, SMS)
                    foreach (var strategy in _strategies)
                    {
                        // 💡 這裡就會呼叫到您的 EmailNotificationStrategy.SendAlertAsync
                        await ExecuteStrategyWithRetryAsync(strategy, message, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "處理告警廣播調度時發生未預期異常。AlertId: {AlertId}", message.AlertId);
                }
            }
        }

        private async Task ExecuteStrategyWithRetryAsync(
            INotificationStrategy strategy,
            AlertMessage message,
            CancellationToken ct)
        {
            try
            {
                _logger.LogInformation("開始透過管道 [{Channel}] 發送告警通知。AlertId: {AlertId}", strategy.ChannelName, message.AlertId);
                await strategy.SendAlertAsync(message, ct);
            }
            catch (Exception ex)
            {
                // 某一管道失敗（如 SMTP 斷線）不應阻斷其他管道（如 Teams）的發送
                _logger.LogError(ex, "通知策略 [{Channel}] 執行失敗。AlertId: {AlertId}", strategy.ChannelName, message.AlertId);
            }
        }
    }
}
