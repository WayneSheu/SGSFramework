#nullable enable
using Serilog.Events;

namespace SGSFramework.Core.Logging
{
    /// <summary>未安裝 SGS.Modules.LOG 時的預設系統日誌儲存：落檔緩衝，外掛載入後回補。</summary>
    public sealed class NullSystemLogStore : ISystemLogStore
    {
        public Task WriteBatchAsync(IReadOnlyList<LogEvent> events, CancellationToken cancellationToken)
            => FileLogBuffer.WriteAsync(FileLogBuffer.SystemKind, events, cancellationToken);
    }

    /// <summary>未安裝 SGS.Modules.LOG 時的預設資安日誌儲存：落檔緩衝（保留完整結構化欄位）。</summary>
    public sealed class NullSecurityLogStore : ISecurityLogStore
    {
        public Task WriteBatchAsync(IReadOnlyList<LogEvent> events, CancellationToken cancellationToken)
            => FileLogBuffer.WriteAsync(FileLogBuffer.SecurityKind, events, cancellationToken);
    }
}
