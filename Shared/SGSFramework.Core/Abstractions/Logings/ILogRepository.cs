namespace SGSFramework.Core.Abstractions.Logings
{
    public interface ILogRepository
    {


        /// <summary>
        /// 批次寫入日誌，適合 BackgroundService 提高效能
        /// </summary>
        Task SaveBatchAsync(IEnumerable<LogEntry> entries, CancellationToken ct = default);

        // 提供一個驗證 Ledger 完整性的方法
        Task<bool> VerifyLedgerIntegrityAsync();
    }
}
