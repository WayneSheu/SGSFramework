using SGSFramework.AuditLog.DTOs;
using System.Threading.Channels;

namespace SGSFramework.AuditLog.Channels
{
    public class AuditChannel
    {
        // 設定容量限制，避免記憶體無限膨脹 (例如最多緩衝 5000 筆)
        // FullMode.Wait: 如果滿了，生產者(Interceptor)會等待，直到有空間
        // FullMode.DropOldest: 如果滿了，丟棄舊日誌 (視業務重要性決定)
        private readonly Channel<AuditEntry> _channel = Channel.CreateBounded<AuditEntry>(new BoundedChannelOptions(5000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true, // 只有一個 BackgroundService 在讀
            SingleWriter = false // 多個 Request 同時在寫
        });

        // 寫入方法 (給 Interceptor 用)
        public async ValueTask AddAuditLogAsync(AuditEntry entry, CancellationToken ct = default)
        {
            await _channel.Writer.WriteAsync(entry, ct);
        }

        // 批次寫入方法
        public async ValueTask AddBatchAuditLogAsync(IEnumerable<AuditEntry> entries, CancellationToken ct = default)
        {
            try
            {
                foreach (var entry in entries)
                {
                    await _channel.Writer.WriteAsync(entry, ct);
                }
            }
            catch (Exception ex)
            {
                _channel.Writer.TryComplete(ex); // 發生例外時，標記通道為完成狀態，並傳遞例外
            }
        }

        // 新增：同步嘗試寫入 (給 SaveChanges 用)
        public bool TryAddAuditLog(AuditEntry entry)
        {
            // TryWrite 是非阻塞的。如果通道滿了，它會回傳 false (可能會掉資料)
            // 對於同步方法，為了避免 Deadlock，通常建議使用 TryWrite
            return _channel.Writer.TryWrite(entry);
        }

        // 如果你堅持同步方法也要保證寫入 (可能會阻塞執行緒)
        public void AddAuditLogSync(AuditEntry entry)
        {
            // 這是阻塞式寫入，直到有空間為止
            var task = _channel.Writer.WriteAsync(entry).AsTask();
            task.Wait();
        }

        // 讀取器 (給 Background Service 用)
        public ChannelReader<AuditEntry> Reader => _channel.Reader;
    
    }
}
