// Path: Infrastructure/SGSFramework.SystemLog/BackgroundServices/SystemLogPersistentWorker.cs
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
    /// 常駐背景服務：專職排空 SystemLogChannel，透過 <see cref="ISystemLogStore"/> 寫入。
    /// store 由 SGS.Modules.LOG 外掛提供；失敗或未安裝時落檔緩衝（Log_Buffer），恢復後自動回補。
    /// </summary>
    public sealed class SystemLogPersistentWorker : BackgroundService
    {
        private static readonly TimeSpan ReplayInterval = TimeSpan.FromSeconds(60);

        private readonly SystemLogChannel _channel;
        private readonly ISystemLogStore _store;
        private readonly ILogger<SystemLogPersistentWorker> _logger;
        private DateTime _lastReplayUtc = DateTime.MinValue;

        public SystemLogPersistentWorker(
            SystemLogChannel channel,
            ISystemLogStore store,
            ILogger<SystemLogPersistentWorker> logger)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SystemLogPersistentWorker 已啟動，儲存實作：{Store}", _store.GetType().Name);

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

                    while (batch.Count < 100 && _channel.Reader.TryRead(out var logEvent))
                    {
                        batch.Add(logEvent);
                    }

                    if (batch.Count > 0)
                    {
                        await _store.WriteBatchAsync(batch, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // 關機時把已取出尚未寫入的批次落檔，避免遺失
                    if (batch.Count > 0) await FileLogBuffer.WriteAsync(FileLogBuffer.SystemKind, batch);
                    break;
                }
                catch (Exception ex)
                {
                    // 不使用 ILogger 寫錯誤（會再進同一 channel 造成回圈），僅 SelfLog + 落檔
                    Serilog.Debugging.SelfLog.WriteLine($"[SystemLog] 批次寫入失敗，改落檔緩衝: {ex.Message}");
                    await FileLogBuffer.WriteAsync(FileLogBuffer.SystemKind, batch);
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ContinueWith(_ => { });
                }
            }
        }

        private async Task TryReplayBufferAsync(CancellationToken ct)
        {
            if (_store is NullSystemLogStore) return; // Null store 只會再寫回緩衝，無意義
            if (DateTime.UtcNow - _lastReplayUtc < ReplayInterval) return;
            _lastReplayUtc = DateTime.UtcNow;

            if (FileLogBuffer.ListFiles(FileLogBuffer.SystemKind).Count == 0) return;
            var count = await FileLogBuffer.ReplayAsync(FileLogBuffer.SystemKind, _store.WriteBatchAsync, ct);
            if (count > 0) _logger.LogInformation("系統日誌緩衝已回補 {Count} 筆", count);
        }
    }
}
