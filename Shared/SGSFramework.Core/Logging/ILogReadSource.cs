#nullable enable
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Abstractions.Logings;

namespace SGSFramework.Core.Logging
{
    /// <summary>可查詢的日誌來源（熱庫或冷庫）。</summary>
    public sealed record LogSourceInfo(string Key, string DisplayName, bool IsHot);

    /// <summary>
    /// 日誌讀取來源（唯讀查詢串流）。由 SGS.Modules.LOG 外掛以獨立日誌庫實作（熱庫 + 設定的冷庫）；
    /// 日誌模組未載入 / 未設定時不註冊，查詢服務回報「日誌模組未啟用」。
    /// 串流來自 Scoped 的 DbContext，僅限在同一請求內列舉；請求結束時連同冷庫連線一併釋放。
    /// </summary>
    public interface ILogReadSource
    {
        /// <summary>可查詢的來源（熱庫在前，冷庫依設定順序）</summary>
        IReadOnlyList<LogSourceInfo> Sources { get; }

        /// <summary>熱庫（現行）串流</summary>
        IQueryable<SystemLog> SystemLogs { get; }

        IQueryable<SecurityLog> SecurityLogs { get; }

        IQueryable<AuditLogEntity> AuditLogs { get; }

        /// <summary>指定來源的串流；空值 = 熱庫；未知來源擲出 <see cref="ArgumentException"/>。</summary>
        IQueryable<SystemLog> SystemLogsOf(string? sourceKey);

        IQueryable<SecurityLog> SecurityLogsOf(string? sourceKey);

        IQueryable<AuditLogEntity> AuditLogsOf(string? sourceKey);

        /// <summary>
        /// 熱庫各資料表的近似筆數（sys.partitions；key 為 "schema.table"，例如 "log.SystemLogs"）。
        /// 避免對大型帳本表 COUNT(*) 全表掃描。
        /// </summary>
        Task<IReadOnlyDictionary<string, long>> GetApproximateRowCountsAsync(CancellationToken cancellationToken);
    }
}
