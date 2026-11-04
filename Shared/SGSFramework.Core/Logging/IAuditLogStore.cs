#nullable enable
using SGSFramework.Core.Abstractions.Entities.AuditLogs;

namespace SGSFramework.Core.Logging
{
    /// <summary>
    /// 資料日誌（稽核）儲存契約。由 SGS.Modules.LOG 外掛實作：寫入獨立日誌庫並以 <c>sourceKey</c> 分鏈計算雜湊。
    /// 未註冊時，AuditWorker 沿用各模組 DbContext 的既有儲存策略（向下相容）。
    /// </summary>
    public interface IAuditLogStore
    {
        /// <summary>
        /// 寫入單批次稽核紀錄。
        /// </summary>
        /// <param name="sourceKey">來源識別（預設為產生稽核的 DbContext 型別名稱，如 PhysLIMSDbContext、ORGDbContext）</param>
        /// <param name="batch">稽核紀錄；PreviousHash/StoredHash 由實作端計算</param>
        Task SaveBatchAsync(string sourceKey, IReadOnlyList<AuditLogEntity> batch, CancellationToken cancellationToken);
    }
}
