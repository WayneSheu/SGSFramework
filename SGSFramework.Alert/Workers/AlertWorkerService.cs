using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGSFramework.Alert.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;

namespace SGSFramework.Alert.Workers
{
    /// <summary>
    /// 高可用告警背景消費服務 (具備例外隔離保護機制)
    /// </summary>
    public sealed class AlertWorkerService : BackgroundService
    {
        private readonly ChannelReader<AlertMessageModel> _channelReader;
        private readonly IAlertDispatcherService _dispatcherService;
        private readonly ILogger<AlertWorkerService> _logger;

        public AlertWorkerService(
            ChannelReader<AlertMessageModel> channelReader,
            IAlertDispatcherService dispatcherService,
            ILogger<AlertWorkerService> logger)
        {
            _channelReader = channelReader ?? throw new ArgumentNullException(nameof(channelReader));
            _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AlertWorkerService 啟動，開始監聽日誌告警佇列...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 讀取佇列訊息，防止未捕獲例外中斷主循環
                    while (await _channelReader.WaitToReadAsync(stoppingToken))
                    {
                        while (_channelReader.TryRead(out var alert))
                        {
                            await ProcessAlertSafeAsync(alert, stoppingToken);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("AlertWorkerService 收到停止訊號，正在優雅關閉...");
                    break;
                }
                catch (Exception ex)
                {
                    // 頂層未捕獲例外防護，防止崩潰進程 (Process Exit -1)
                    _logger.LogCritical(ex, "[AlertWorkerService] 佇列監聽發生嚴重例外，服務自動恢復並繼續重試...");
                    await Task.Delay(1000, stoppingToken); // 避免死循環高佔用 CPU
                }
            }
        }

        private async Task ProcessAlertSafeAsync(AlertMessageModel alert, CancellationToken cancellationToken)
        {
            try
            {
                await _dispatcherService.DispatchAsync(alert, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AlertWorkerService] 處理單筆告警訊息時發生例外。AlertId: {AlertId}", alert.AlertId);
            }
        }
    }
}
