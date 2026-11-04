// Path: Infrastructure/SGSFramework.SystemLog/BackgroundServices/SecurityLogPersistentWorker.cs
#nullable enable
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using SGSFramework.Core.Logging;
using SGSFramework.SystemLog.Channels;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.SystemLog.BackgroundServices
{
    /// <summary>
    /// 常駐背景服務：監聽並排空 SecurityLogChannel，透過 <see cref="ISecurityLogStore"/> 寫入；
    /// 失敗或外掛未安裝時落檔緩衝（保留完整結構化欄位），恢復後自動回補。
    /// </summary>
    public sealed class SecurityLogPersistentWorker : BackgroundService
    {
        private static readonly TimeSpan ReplayInterval = TimeSpan.FromSeconds(60);

        private readonly SecurityLogChannel _channel;
        private readonly ISecurityLogStore _store;
        private readonly ILogger<SecurityLogPersistentWorker> _logger;
        private DateTime _lastReplayUtc = DateTime.MinValue;

        public SecurityLogPersistentWorker(
            SecurityLogChannel channel,
            ISecurityLogStore store,
            ILogger<SecurityLogPersistentWorker> logger)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SecurityLogPersistentWorker 已啟動，儲存實作：{Store}", _store.GetType().Name);

            while (!stoppingToken.IsCancellationRequested)
            {
                var batch = new List<LogEvent>();
                try
                {
                    await TryReplayBufferAsync(stoppingToken);

                    using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    waitCts.CancelAfter(ReplayInterval);
                    bool hasData;
                    try { hasData = await _channel.Reader.WaitToReadAsync(waitCts.Token); }
                    catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested) { continue; }

                    if (!hasData) continue;

                    while (batch.Count < 50 && _channel.Reader.TryRead(out var securityEvent))
                    {
                        batch.Add(securityEvent);
                    }

                    if (batch.Count > 0)
                    {
                        await _store.WriteBatchAsync(batch, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    if (batch.Count > 0) await FileLogBuffer.WriteAsync(FileLogBuffer.SecurityKind, batch);
                    break;
                }
                catch (Exception ex)
                {
                    Serilog.Debugging.SelfLog.WriteLine($"[SecurityLog] 批次寫入失敗，改落檔緩衝: {ex.Message}");
                    await FileLogBuffer.WriteAsync(FileLogBuffer.SecurityKind, batch);
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ContinueWith(_ => { });
                }
            }
        }

        private async Task TryReplayBufferAsync(CancellationToken ct)
        {
            if (_store is NullSecurityLogStore) return;
            if (DateTime.UtcNow - _lastReplayUtc < ReplayInterval) return;
            _lastReplayUtc = DateTime.UtcNow;

            if (FileLogBuffer.ListFiles(FileLogBuffer.SecurityKind).Count == 0) return;
            var count = await FileLogBuffer.ReplayAsync(FileLogBuffer.SecurityKind, _store.WriteBatchAsync, ct);
            if (count > 0) _logger.LogInformation("資安日誌緩衝已回補 {Count} 筆", count);
        }
    }
}
