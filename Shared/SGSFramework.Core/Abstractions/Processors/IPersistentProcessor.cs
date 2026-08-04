namespace SGSFramework.Core.Abstractions.Processors
{
    public interface IPersistentProcessor<T>
    {
        // 定義批次寫入邏輯（如 SQL Bulk Copy）
        Task ProcessBatchAsync(IEnumerable<T> items, CancellationToken ct);

        // 定義失敗時的溢流邏輯（如寫入 Local File）
        Task FallbackAsync(IEnumerable<T> items, Exception ex);
    }
}
