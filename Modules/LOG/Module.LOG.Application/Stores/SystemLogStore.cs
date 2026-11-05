using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Serilog.Events;
using SGS.Modules.LOG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Helpers;
using SGSFramework.Core.Logging;
using SGSFramework.SystemLog.Extensions;

namespace SGS.Modules.LOG.Application.Stores;

/// <summary>系統日誌 → 獨立日誌庫 log.SystemLogs（SqlBulkCopy）。失敗擲例外，由 Host Worker 落檔緩衝並於恢復後回補。</summary>
public sealed class SystemLogStore : ISystemLogStore
{
    private readonly IDbContextFactory<LogDbContext> _factory;
    private readonly ILogIdentityResolver? _resolver;

    public SystemLogStore(IDbContextFactory<LogDbContext> factory, ILogIdentityResolver? resolver = null)
    {
        _factory = factory;
        _resolver = resolver;
    }

    public async Task WriteBatchAsync(IReadOnlyList<LogEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0) return;

        var (users, tenants) = await ResolveNamesAsync(events, cancellationToken).ConfigureAwait(false);
        using var table = BuildTable(events, users, tenants);

        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var connection = (SqlConnection)db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.FireTriggers, null)
            {
                DestinationTableName = "[log].[SystemLogs]",
                BatchSize = table.Rows.Count,
                BulkCopyTimeout = 30
            };
            foreach (DataColumn c in table.Columns) bulk.ColumnMappings.Add(c.ColumnName, c.ColumnName);
            await bulk.WriteToServerAsync(table, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    private async Task<(IReadOnlyDictionary<string, string>, IReadOnlyDictionary<string, string>)> ResolveNamesAsync(
        IReadOnlyList<LogEvent> events, CancellationToken ct)
    {
        var empty = new Dictionary<string, string>();
        if (_resolver is null) return (empty, empty);

        var userIds = events.Select(e => LogEventValues.Text(e, "UserId"))
            .Where(id => !string.IsNullOrEmpty(id) && !id!.StartsWith("SYSTEM", StringComparison.OrdinalIgnoreCase))
            .Select(id => Guid.TryParse(id, out var g) ? g : (Guid?)null)
            .Where(g => g.HasValue).Select(g => g!.Value).Distinct().ToList();
        var tenantIds = events.Select(e => LogEventValues.Text(e, "TenantId"))
            .Where(id => !string.IsNullOrEmpty(id)).Cast<string>().Distinct().ToList();

        return await _resolver.ResolveAsync(userIds, tenantIds, ct).ConfigureAwait(false);
    }

    private static DataTable BuildTable(
        IReadOnlyList<LogEvent> events, IReadOnlyDictionary<string, string> users, IReadOnlyDictionary<string, string> tenants)
    {
        var dt = new DataTable();
        dt.Columns.Add("TimeStamp", typeof(DateTimeOffset));
        dt.Columns.Add("CreatedAt", typeof(DateTimeOffset));
        foreach (var name in new[] { "Message", "Level", "Exception", "TenantId", "TenantName", "UserId", "UserName", "ModuleName",
                                     "Operation", "OperationDesc", "CorrelationId", "IP", "Url", "Payload", "AlertId", "Fingerprint" })
            dt.Columns.Add(name, typeof(string));

        foreach (var e in events)
        {
            var row = dt.NewRow();
            row["TimeStamp"] = e.Timestamp != default ? DateTimeHelper.ConvertToTaiwanTime(e.Timestamp) : DateTimeHelper.GetTaiwanTime();
            row["CreatedAt"] = DateTimeHelper.GetTaiwanTime();
            row["Message"] = e.RenderMessage();
            row["Level"] = e.Level.ToString();
            row["Exception"] = LogEventValues.Db(e.Exception?.ToString());

            var tenantId = LogEventValues.Text(e, "TenantId");
            row["TenantId"] = LogEventValues.Db(tenantId);
            row["TenantName"] = !string.IsNullOrEmpty(tenantId) && tenants.TryGetValue(tenantId, out var tn) ? tn : DBNull.Value;

            var userId = LogEventValues.Text(e, "UserId");
            var userName = LogEventValues.Text(e, "UserName");
            row["UserId"] = LogEventValues.Db(userId);
            row["UserName"] =
                !string.IsNullOrEmpty(userId) && users.TryGetValue(userId, out var real) ? real
                : !string.IsNullOrEmpty(userName) && !userName.Equals("System", StringComparison.OrdinalIgnoreCase) ? userName
                : !string.IsNullOrEmpty(userId) && !userId.Equals("System", StringComparison.OrdinalIgnoreCase) ? userId
                : "系統行程";

            row["ModuleName"] = LogEventValues.Db(LogEventValues.Text(e, "ModuleName"));
            row["Operation"] = LogEventValues.Db(LogEventValues.Text(e, "Operation"));
            row["OperationDesc"] = LogEventValues.Db(LogEventValues.Text(e, "OperationDesc"));
            row["CorrelationId"] = LogEventValues.Db(LogEventValues.Text(e, "CorrelationId"));
            row["IP"] = LogEventValues.Db(LogEventValues.Text(e, "IP"));
            row["Url"] = LogEventValues.Db(LogEventValues.Text(e, "Url"));
            row["Payload"] = LogEventPropertySerializer.Serialize(e.Properties);
            row["AlertId"] = LogEventValues.Db(LogEventValues.Text(e, "AlertId"));
            row["Fingerprint"] = LogEventValues.Db(LogEventValues.Text(e, "Fingerprint"));
            dt.Rows.Add(row);
        }

        return dt;
    }
}
