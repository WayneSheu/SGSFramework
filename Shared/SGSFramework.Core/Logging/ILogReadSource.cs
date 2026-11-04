#nullable enable
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Abstractions.Logings;

namespace SGSFramework.Core.Logging
{
    /// <summary>
    /// 日誌讀取來源（唯讀查詢串流）。由 SGS.Modules.LOG 外掛以獨立日誌庫實作；
    /// 未安裝或未啟用時不註冊，Host 的查詢服務退回讀主庫舊日誌表。
    /// 串流來自 Scoped DbContext，僅限在同一請求內列舉。
    /// </summary>
    public interface ILogReadSource
    {
        IQueryable<SystemLog> SystemLogs { get; }

        IQueryable<SecurityLog> SecurityLogs { get; }

        IQueryable<AuditLogEntity> AuditLogs { get; }

        /// <summary>
        /// 日誌庫各資料表的近似筆數（sys.partitions；key 為 "schema.table"，例如 "log.SystemLogs"）。
        /// 避免對大型帳本表 COUNT(*) 全表掃描。
        /// </summary>
        Task<IReadOnlyDictionary<string, long>> GetApproximateRowCountsAsync(CancellationToken cancellationToken);
    }
}
