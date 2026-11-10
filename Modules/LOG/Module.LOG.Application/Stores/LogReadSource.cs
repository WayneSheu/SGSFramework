using Microsoft.EntityFrameworkCore;
using SGS.Modules.LOG.Infrastructure.Dbcontexts;
using SGS.Modules.LOG.Infrastructure.Services;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Abstractions.Logings;
using SGSFramework.Core.Logging;

namespace SGS.Modules.LOG.Application.Stores;

/// <summary>
/// 獨立日誌庫的唯讀查詢來源（Scoped，隨請求釋放）：熱庫使用 DI 的 <see cref="LogDbContext"/>，
/// 冷庫於需要時才建立連線（<see cref="LogColdContextFactory"/>），請求結束時一併釋放。
/// </summary>
public sealed class LogReadSource : ILogReadSource, IDisposable
{
    private readonly LogDbContext _hot;
    private readonly ILogTierCatalog _catalog;
    private readonly LogColdContextFactory _coldFactory;
    private readonly Dictionary<string, LogDbContext> _cold = new(StringComparer.OrdinalIgnoreCase);

    public LogReadSource(LogDbContext hot, ILogTierCatalog catalog, LogColdContextFactory coldFactory)
    {
        _hot = hot;
        _catalog = catalog;
        _coldFactory = coldFactory;
        Sources = catalog.All.Select(t => new LogSourceInfo(t.Key, t.DisplayName, t.IsHot)).ToList();
    }

    public IReadOnlyList<LogSourceInfo> Sources { get; }

    public IQueryable<SystemLog> SystemLogs => _hot.SystemLogs.AsNoTracking();

    public IQueryable<SecurityLog> SecurityLogs => _hot.SecurityLogs.AsNoTracking();

    public IQueryable<AuditLogEntity> AuditLogs => _hot.AuditLogs.AsNoTracking();

    public IQueryable<SystemLog> SystemLogsOf(string? sourceKey) => Context(sourceKey).SystemLogs.AsNoTracking();

    public IQueryable<SecurityLog> SecurityLogsOf(string? sourceKey) => Context(sourceKey).SecurityLogs.AsNoTracking();

    public IQueryable<AuditLogEntity> AuditLogsOf(string? sourceKey) => Context(sourceKey).AuditLogs.AsNoTracking();

    private LogDbContext Context(string? sourceKey)
    {
        var tier = _catalog.Find(sourceKey) ?? throw new ArgumentException($"找不到日誌資料庫來源：{sourceKey}", nameof(sourceKey));
        if (tier.IsHot) return _hot;

        if (!_cold.TryGetValue(tier.Key, out var db))
        {
            db = _coldFactory.Create(tier.Key);
            _cold[tier.Key] = db;
        }

        return db;
    }

    public async Task<IReadOnlyDictionary<string, long>> GetApproximateRowCountsAsync(CancellationToken cancellationToken)
    {
        var rows = await _hot.Database.SqlQueryRaw<TableRows>(
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

    public void Dispose()
    {
        foreach (var db in _cold.Values) db.Dispose();
        _cold.Clear();
    }

    private sealed class TableRows
    {
        public string Name { get; set; } = string.Empty;

        public long Rows { get; set; }
    }
}
