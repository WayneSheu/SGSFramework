namespace SGSFramework.Core.Abstractions.Outbox
{
    public interface IOutboxRepository
    {
        // 原子化抓取：由 DbContext 實作 SQL UPDATE...OUTPUT
        Task<List<OutboxMessage>> FetchOutboxMessagesAsync(int batchSize);

        // 儲存狀態變更
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
