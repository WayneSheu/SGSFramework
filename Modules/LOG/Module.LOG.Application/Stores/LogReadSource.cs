using Microsoft.EntityFrameworkCore;
using SGS.Modules.LOG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Abstractions.Logings;
using SGSFramework.Core.Logging;


namespace SGS.Modules.LOG.Application.Stores;

/// <summary>獨立日誌庫的唯讀查詢來源（Scoped，隨請求釋放）。</summary>
public sealed class LogReadSource : ILogReadSource
{
    private readonly LogDbContext _db;

    public LogReadSource(LogDbContext db) => _db = db;

    public IQueryable<SystemLog> SystemLogs => _db.SystemLogs.AsNoTracking();

    public IQueryable<SecurityLog> SecurityLogs => _db.SecurityLogs.AsNoTracking();

    public IQueryable<AuditLogEntity> AuditLogs => _db.AuditLogs.AsNoTracking();

    public async Task<IReadOnlyDictionary<string, long>> GetApproximateRowCountsAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.Database.SqlQueryRaw<TableRows>(
                """
                SELECT s.name + N'.' + t.name AS [Name], CAST(SUM(p.rows) AS bigint) AS [Rows]
                FROM sys.tables t
                JOIN sys.schemas s ON s.schema_id = t.schema_id
                JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
                WHERE s.name = N'log'
                GROUP BY s.name, t.name
                """)
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.Name, r => r.Rows, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class TableRows
    {
        public string Name { get; set; } = string.Empty;

        public long Rows { get; set; }
    }
}
