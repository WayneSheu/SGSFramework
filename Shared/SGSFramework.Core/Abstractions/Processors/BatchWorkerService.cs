using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;

namespace SGSFramework.Core.Abstractions.Processors
{
    public class BatchWorkerService<T> : BackgroundService
    {
        private readonly GenericChannel<T> _channel;
        private readonly IPersistentProcessor<T> _processor;
        private readonly ILogger<BatchWorkerService<T>> _logger;

        public BatchWorkerService(
            GenericChannel<T> channel,
            IPersistentProcessor<T> processor,
            ILogger<BatchWorkerService<T>> logger)
        {
            _channel = channel;
            _processor = processor;
            _logger = logger;
        }

        //protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        //{
        //    // 這裡不直接在 while 判斷 stoppingToken，因為停止時我們還要繼續讀
        //    try
        //    {
        //        // ReadAllAsync 會持續讀取直到 Channel.Writer.Complete() 被呼叫且資料讀完
        //        await foreach (var item in _channel.Reader.ReadAllAsync(CancellationToken.None))
        //        {
        //            var batch = new List<T> { item };

        //            // 嘗試湊齊批次 (避免關閉時等待太久，縮短批次大小或時間)
        //            while (batch.Count < 50 && _channel.Reader.TryRead(out var extraItem))
        //            {
        //                batch.Add(extraItem);
        //            }

        //            await ExecuteWithPolicy(batch, CancellationToken.None);

        //            // 若已觸發停止訊號且通道已空，則提早跳出
        //            if (stoppingToken.IsCancellationRequested && _channel.Reader.Count == 0)
        //                break;
        //        }
        //    }
        //    catch (OperationCanceledException) { /* 正常關閉 */ }
        //    finally
        //    {
        //        _logger.LogInformation("Log Background Service has drained all logs and is shutting down.");
        //    }
        //}

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                // 使用 ReadAllAsync，它會在 Channel.Writer.Complete() 後繼續跑直到空
                await foreach (var item in _channel.Reader.ReadAllAsync(CancellationToken.None))
                {
                    var batch = new List<T> { item };

                    // 盡可能收集現有資料
                    while (_channel.Reader.TryRead(out var extra)) batch.Add(extra);

                    // 【關鍵】寫入時不要使用 stoppingToken
                    // 因為進入關閉流程時 stoppingToken 已經 Cancel 了
                    // 我們改用一個帶有自定義時限的自建 Token
                    using var writeCts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                    await ExecuteWithPolicy(batch, writeCts.Token);
                }
            }
            catch (Exception ex)
            {
                // 確保最後的最後能記錄到系統日誌
                Serilog.Debugging.SelfLog.WriteLine($"Shutdown error: {ex}");
            }
        }

        private async Task ExecuteWithPolicy(List<T> items, CancellationToken ct)
        {
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(Math.Pow(2, i)));

            try
            {
                await retryPolicy.ExecuteAsync(async () => await _processor.ProcessBatchAsync(items, ct));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Persistent failure after retries. Executing fallback.");
                await _processor.FallbackAsync(items, ex);
            }
        }
    }
}
