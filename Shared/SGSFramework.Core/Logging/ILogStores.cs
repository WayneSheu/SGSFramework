#nullable enable
using Serilog.Events;

namespace SGSFramework.Core.Logging
{
    /// <summary>
    /// 系統日誌儲存契約。Host 的 Serilog 管線只依賴此介面；
    /// 實作由 SGS.Modules.LOG 外掛提供，未安裝時使用 <see cref="NullSystemLogStore"/>（落檔緩衝）。
    /// </summary>
    public interface ISystemLogStore
    {
        /// <summary>寫入一批系統日誌。失敗時應擲出例外，由呼叫端改走檔案緩衝。</summary>
        Task WriteBatchAsync(IReadOnlyList<LogEvent> events, CancellationToken cancellationToken);
    }

    /// <summary>資安日誌儲存契約（同 <see cref="ISystemLogStore"/> 設計）。</summary>
    public interface ISecurityLogStore
    {
        Task WriteBatchAsync(IReadOnlyList<LogEvent> events, CancellationToken cancellationToken);
    }
}
