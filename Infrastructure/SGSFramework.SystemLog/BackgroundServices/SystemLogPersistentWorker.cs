// Path: Infrastructure/SGSFramework.SystemLog/BackgroundServices/SystemLogPersistentWorker.cs
#nullable enable
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using SGSFramework.SystemLog.Channels;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.SystemLog.BackgroundServices
{
    /// <summary>
    /// 常駐背景服務：專職排空 SystemLogChannel，並透過批次處理器非同步寫入至資料庫中。
    /// </summary>
    public sealed class SystemLogPersistentWorker : BackgroundService
    {
        private readonly SystemLogChannel _channel;
        // 💡 核心修正：將 IPersistentProcessor<LogEvent> 改為具體類別 SqlServerLogProcessor
        private readonly SqlServerLogProcessor _processor;
        private readonly ILogger<SystemLogPersistentWorker> _logger;

        public SystemLogPersistentWorker(
            SystemLogChannel channel,
            SqlServerLogProcessor processor, // 💡 精準具體注入
            ILogger<SystemLogPersistentWorker> logger)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SystemLogPersistentWorker 基礎設施背景消費端已成功啟動。");

            while (!stoppingToken.IsCancellationRequested)
            {
                var batch = new List<LogEvent>();
                try
                {
                    if (await _channel.Reader.WaitToReadAsync(stoppingToken))
                    {
                        while (batch.Count < 100 && _channel.Reader.TryRead(out var logEvent))
                        {
                            batch.Add(logEvent);
                        }

                        if (batch.Count > 0)
                        {
                            await _processor.ProcessBatchAsync(batch, stoppingToken);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("SystemLogPersistentWorker 收到安全停止訊號，即將關閉服務。");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SystemLogPersistentWorker 在批次處理期間發生未預期異常。 即將進入降級 Fallback 流程。");

                    try
                    {
                        await _processor.FallbackAsync(batch, ex);
                    }
                    catch (Exception fallbackEx)
                    {
                        Serilog.Debugging.SelfLog.WriteLine($"[Critical] SystemLog Failure Fallback 也潰堤: {fallbackEx.Message}");
                    }
                }
            }
        }
    }
}