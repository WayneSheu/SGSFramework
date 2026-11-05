using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Serilog.Events;
using SGS.Modules.LOG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Helpers;
using SGSFramework.Core.Logging;

namespace SGS.Modules.LOG.Application.Stores;

/// <summary>資安日誌 → 獨立日誌庫 log.SecurityLog，整批單一交易（全有或全無，回補時不會重複）。</summary>
public sealed class SecurityLogStore : ISecurityLogStore
{
    private static readonly string[] TextColumns =
        ["Message", "Level", "Exception", "LogType", "EventCategory", "UserId", "ClientIp", "CorrelationId", "AlertId", "Fingerprint"];

    private readonly IDbContextFactory<LogDbContext> _factory;

    public SecurityLogStore(IDbContextFactory<LogDbContext> factory) => _factory = factory;

    public async Task WriteBatchAsync(IReadOnlyList<LogEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0) return;

        using var table = new DataTable();
        table.Columns.Add("Timestamp", typeof(DateTimeOffset));
        foreach (var c in TextColumns) table.Columns.Add(c, typeof(string));

        foreach (var e in events)
        {
            var row = table.NewRow();
            row["Timestamp"] = e.Timestamp != default ? DateTimeHelper.ConvertToTaiwanTime(e.Timestamp) : DateTimeHelper.GetTaiwanTime();
            row["Message"] = e.RenderMessage();
            row["Level"] = e.Level.ToString();
            row["Exception"] = LogEventValues.Db(e.Exception?.ToString());
            foreach (var key in new[] { "LogType", "EventCategory", "UserId", "ClientIp", "CorrelationId", "AlertId", "Fingerprint" })
                row[key] = LogEventValues.Db(LogEventValues.Text(e, key));
            table.Rows.Add(row);
        }

        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var connection = (SqlConnection)db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var bulk = new SqlBulkCopy(connection,
                SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.FireTriggers | SqlBulkCopyOptions.UseInternalTransaction, null)
            {
                DestinationTableName = "[log].[SecurityLog]",
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
}
