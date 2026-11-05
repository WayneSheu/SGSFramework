using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SGS.Modules.LOG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Logging;

namespace SGS.Modules.LOG.Application.Stores;

/// <summary>
/// 資料日誌 → 獨立日誌庫 log.AuditLogs。
/// 雜湊鏈以 <see cref="AuditLogEntity.Schema"/>（core/org/fms/rpt/sch…）分鏈；
/// 「讀上一筆雜湊 + 寫入」在同一交易內，並以 sp_getapplock 對該 Schema 互斥，避免多實例同時寫入造成鏈分叉。
/// </summary>
public sealed class AuditLogStore : IAuditLogStore
{
    private readonly IDbContextFactory<LogDbContext> _factory;

    public AuditLogStore(IDbContextFactory<LogDbContext> factory) => _factory = factory;

    public async Task SaveBatchAsync(string sourceKey, IReadOnlyList<AuditLogEntity> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0) return;

        await using var db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entityType = db.Model.FindEntityType(typeof(AuditLogEntity))
            ?? throw new InvalidOperationException("AuditLogEntity 未設定於 LogDbContext。");
        var store = StoreObjectIdentifier.Table(entityType.GetTableName()!, entityType.GetSchema());

        // Id 由 IDENTITY 產生；僅取有 CLR 屬性之欄位，避開 Ledger 陰影欄位
        var columns = entityType.GetProperties()
            .Where(p => p.PropertyInfo is not null && p.Name != nameof(AuditLogEntity.Id))
            .Select(p => (Property: p, Column: p.GetColumnName(store)!))
            .ToList();

        var connection = (SqlConnection)db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var tx = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            foreach (var group in batch.GroupBy(l => l.Schema ?? "core").OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                await AcquireChainLockAsync(connection, tx, group.Key, cancellationToken).ConfigureAwait(false);
                var previousHash = await ReadLastHashAsync(connection, tx, group.Key, cancellationToken).ConfigureAwait(false);

                foreach (var log in group)
                {
                    Sanitize(log);
                    log.PreviousHash = previousHash;
                    log.StoredHash = CalculateHash(log, previousHash);
                    previousHash = log.StoredHash;
                }
            }

            using var table = new DataTable();
            foreach (var (property, column) in columns)
                table.Columns.Add(column, Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType);

            // 保持呼叫端順序寫入（同 Schema 內順序即鏈順序）
            foreach (var log in batch)
            {
                var row = table.NewRow();
                foreach (var (property, column) in columns)
                    row[column] = property.PropertyInfo!.GetValue(log) ?? DBNull.Value;
                table.Rows.Add(row);
            }

            using var bulk = new SqlBulkCopy(connection,
                SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.FireTriggers, tx)
            {
                DestinationTableName = $"[{store.Schema}].[{store.Name}]",
                BatchSize = batch.Count,
                BulkCopyTimeout = 60
            };
            foreach (var (_, column) in columns) bulk.ColumnMappings.Add(column, column);
            await bulk.WriteToServerAsync(table, cancellationToken).ConfigureAwait(false);

            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    private static async Task AcquireChainLockAsync(SqlConnection connection, SqlTransaction tx, string schema, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(
            "DECLARE @r int; EXEC @r = sp_getapplock @Resource = @res, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; SELECT @r;",
            connection, tx);
        cmd.Parameters.AddWithValue("@res", $"log.AuditLogs.chain.{schema}");
        var result = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false));
        if (result < 0) throw new TimeoutException($"取得稽核雜湊鏈鎖定逾時（schema={schema}, code={result}）。");
    }

    private static async Task<string> ReadLastHashAsync(SqlConnection connection, SqlTransaction tx, string schema, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(
            "SELECT TOP 1 [StoredHash] FROM [log].[AuditLogs] WHERE [Schema] = @s ORDER BY [Id] DESC", connection, tx);
        cmd.Parameters.AddWithValue("@s", schema);
        return (await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false) as string) ?? string.Empty;
    }

    private static void Sanitize(AuditLogEntity log)
    {
        if (!string.IsNullOrWhiteSpace(log.TraceId))
        {
            var cleaned = log.TraceId.Replace("-", string.Empty).Trim();
            log.TraceId = cleaned.Length > 32 ? cleaned[..32] : cleaned.PadRight(32, '0');
        }
        else
        {
            log.TraceId = Guid.NewGuid().ToString("N");
        }

        if (log.UserId?.Length > 128) log.UserId = log.UserId[..128];
        if (log.RemoteIp?.Length > 64) log.RemoteIp = log.RemoteIp[..64];
        if (log.Schema?.Length > 64) log.Schema = log.Schema[..64];
        if (log.TableName?.Length > 128) log.TableName = log.TableName[..128];
        if (log.Action?.Length > 50) log.Action = log.Action[..50];
        if (log.GapReason?.Length > 500) log.GapReason = log.GapReason[..500];
    }

    /// <summary>演算法與 SqlBulkAuditStorageStrategy 相同，確保驗證工具可沿用。</summary>
    private static string CalculateHash(AuditLogEntity log, string previousHash)
    {
        var raw = $"{log.TableName}{log.Action}{log.KeyValues}{log.OldValues}{log.NewValues}{log.Timestamp:o}{previousHash}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }
}
