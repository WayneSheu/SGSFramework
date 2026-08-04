// Path: Infrastructure/SGSFramework.SystemLog/BackgroundServices/SecurityLogPersistentWorker.cs
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
    /// 常駐背景服務：專職監聽並排空 SecurityLogChannel，確保安全敏感稽核日誌無阻塞落庫。
    /// </summary>
    public sealed class SecurityLogPersistentWorker : BackgroundService
    {
        private readonly SecurityLogChannel _channel;
        // 💡 核心修正：確保為具體類別 SqlServerSecurityProcessor 注入
        private readonly SqlServerSecurityProcessor _processor;
        private readonly ILogger<SecurityLogPersistentWorker> _logger;

        public SecurityLogPersistentWorker(
            SecurityLogChannel channel,
            SqlServerSecurityProcessor processor, // 💡 精準具體注入
            ILogger<SecurityLogPersistentWorker> logger)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _processor = processor ?? throw new ArgumentNullException(nameof(processor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SecurityLogPersistentWorker 資安專用日誌消費端已成功啟動。");

            while (!stoppingToken.IsCancellationRequested)
            {
                var batch = new List<LogEvent>();
                try
                {
                    if (await _channel.Reader.WaitToReadAsync(stoppingToken))
                    {
                        while (batch.Count < 50 && _channel.Reader.TryRead(out var securityEvent))
                        {
                            batch.Add(securityEvent);
                        }

                        if (batch.Count > 0)
                        {
                            await _processor.ProcessBatchAsync(batch, stoppingToken);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("SecurityLogPersistentWorker 已安全關閉。");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SecurityLogPersistentWorker 在資安日誌批次寫入期間遭遇失敗。");

                    try
                    {
                        await _processor.FallbackAsync(batch, ex);
                    }
                    catch (Exception fallbackEx)
                    {
                        Serilog.Debugging.SelfLog.WriteLine($"[Critical] SecurityLog Failure Fallback 發生嚴重失敗: {fallbackEx.Message}");
                    }
                }
            }
        }
    }
}