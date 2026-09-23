// ==========================================
// 檔案路徑: Infrastructure/SGSFramework.AuditLog/Abstractions/IAuditChannel.cs
// 架構層級: Domain / Core Abstractions Layer
// ==========================================

namespace SGSFramework.AuditLog.Abstractions;

using SGSFramework.AuditLog.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IAuditChannel<TContext>
{
    ValueTask AddAuditLogAsync(AuditEntry entry, CancellationToken ct = default);
    ValueTask AddBatchAuditLogAsync(IEnumerable<AuditEntry> entries, CancellationToken ct = default);
    bool TryAddAuditLog(AuditEntry entry);
    IAsyncEnumerable<AuditEntry> ReadAllAsync(CancellationToken ct = default);
}