using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.AuditLogs;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.AuditLog.Services.Strategies
{
    public sealed class SqlBulkAuditStorageStrategy<TContext> : IAuditStorageStrategy<TContext>
        where TContext : DbContext
    {
        private readonly ILogger<SqlBulkAuditStorageStrategy<TContext>>? _logger;

        public SqlBulkAuditStorageStrategy(ILogger<SqlBulkAuditStorageStrategy<TContext>>? logger = null)
        {
            _logger = logger;
        }

        public async Task SaveBatchAsync(TContext dbContext, List<AuditLogEntity> batch, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            if (batch is null || batch.Count == 0) return;

            var dbSet = dbContext.Set<AuditLogEntity>();
            if (dbSet is null)
            {
                throw new InvalidOperationException($"Entity '{nameof(AuditLogEntity)}' is not configured in DbContext '{typeof(TContext).Name}'.");
            }

            IQueryable<AuditLogEntity> query = dbSet.AsNoTracking();
            var lastLog = await query
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            string previousHash = lastLog?.StoredHash ?? string.Empty;

            foreach (var log in batch)
            {
                // 防護 1: 規範化並清理 TraceId (去除 '-' 確保符碼為 32 字元，避免 char(32) 溢位)
                SanitizeAuditLog(log);

                log.PreviousHash = previousHash;
                log.StoredHash = CalculateHash(log, previousHash);
                previousHash = log.StoredHash;
            }

            try
            {
                await BulkCopyAsync(dbContext, batch, cancellationToken);
            }
            catch (Exception ex) when (ex is SqlException || ex is DbUpdateException || ex is InvalidOperationException)
            {
                _logger?.LogWarning(ex, "[AuditLog] SqlBulkCopy failed. Fallback to DbContext.SaveChangesAsync.");

                // 備援方案：若 BCP / BulkCopy 失敗，清除 ChangeTracker 並退回標準 Entity Framework 寫入
                await FallbackSaveAsync(dbContext, batch, cancellationToken);
            }
        }

        public async Task RecoverLogsAsync(TContext dbContext, List<AuditLogEntity> fallbackLogs, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(dbContext);
            if (fallbackLogs is null || fallbackLogs.Count == 0) return;

            foreach (var log in fallbackLogs)
            {
                SanitizeAuditLog(log);
            }

            try
            {
                await BulkCopyAsync(dbContext, fallbackLogs, cancellationToken);
            }
            catch (Exception ex) when (ex is SqlException || ex is DbUpdateException || ex is InvalidOperationException)
            {
                _logger?.LogWarning(ex, "[AuditLog Recover] Recover SqlBulkCopy failed. Fallback to standard save.");
                await FallbackSaveAsync(dbContext, fallbackLogs, cancellationToken);
            }
        }

        /// <summary>
        /// 以原生 <see cref="SqlBulkCopy"/> 批次寫入 Ledger 資料表（不依賴第三方 ORM 擴充套件）。
        /// 整批以單一交易寫入，中途失敗即整批回復，因此後續退回標準寫入不會產生重複紀錄。
        /// </summary>
        private static async Task BulkCopyAsync(TContext dbContext, List<AuditLogEntity> logs, CancellationToken cancellationToken)
        {
            var entityType = dbContext.Model.FindEntityType(typeof(AuditLogEntity))
                ?? throw new InvalidOperationException($"Entity '{nameof(AuditLogEntity)}' is not configured in DbContext '{typeof(TContext).Name}'.");

            string tableName = entityType.GetTableName() ?? throw new InvalidOperationException("AuditLogEntity 未對應資料表。");
            string? schema = entityType.GetSchema() ?? entityType.Model.GetDefaultSchema();
            var store = StoreObjectIdentifier.Table(tableName, schema);

            // Id 由資料庫 IDENTITY 產生，不寫入；僅取有 CLR 屬性之欄位，避開 Ledger 陰影欄位 (如 ledger_start_transaction_id)
            var columns = entityType.GetProperties()
                .Where(p => p.PropertyInfo is not null && p.Name != nameof(AuditLogEntity.Id))
                .Select(p => (Property: p, Column: p.GetColumnName(store)!))
                .ToList();

            using var table = new DataTable();
            foreach (var (property, column) in columns)
            {
                table.Columns.Add(column, Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType);
            }

            foreach (var log in logs)
            {
                var row = table.NewRow();
                foreach (var (property, column) in columns)
                {
                    row[column] = property.PropertyInfo!.GetValue(log) ?? DBNull.Value;
                }
                table.Rows.Add(row);
            }

            var connection = (SqlConnection)dbContext.Database.GetDbConnection();
            bool openedHere = connection.State != ConnectionState.Open;
            if (openedHere)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                // 已存在外部交易時沿用之；否則以內部交易包住整批 (BatchSize = 筆數，確保全有或全無)
                var external = (dbContext.Database.CurrentTransaction as IInfrastructure<DbTransaction>)?.Instance as SqlTransaction;
                var options = SqlBulkCopyOptions.TableLock | (external is null ? SqlBulkCopyOptions.UseInternalTransaction : SqlBulkCopyOptions.Default);

                using var bulkCopy = new SqlBulkCopy(connection, options, external)
                {
                    DestinationTableName = string.IsNullOrEmpty(schema) ? $"[{tableName}]" : $"[{schema}].[{tableName}]",
                    BatchSize = logs.Count,
                    BulkCopyTimeout = 60
                };

                foreach (var (_, column) in columns)
                {
                    bulkCopy.ColumnMappings.Add(column, column);
                }

                await bulkCopy.WriteToServerAsync(table, cancellationToken);
            }
            finally
            {
                if (openedHere)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private static void SanitizeAuditLog(AuditLogEntity log)
        {
            // 處理 TraceId (DB: char(32)) - 去除 GUID 中的破折號，若為空補足 32 位元
            if (!string.IsNullOrWhiteSpace(log.TraceId))
            {
                string cleaned = log.TraceId.Replace("-", string.Empty).Trim();
                log.TraceId = cleaned.Length > 32 ? cleaned[..32] : cleaned.PadRight(32, '0');
            }
            else
            {
                log.TraceId = Guid.NewGuid().ToString("N"); // 預設產出 32 位元無 '-' 字串
            }

            // 防護其他可能長度過載的字串欄位
            if (log.UserId?.Length > 128) log.UserId = log.UserId[..128];
            if (log.RemoteIp?.Length > 64) log.RemoteIp = log.RemoteIp[..64];
            if (log.Schema?.Length > 64) log.Schema = log.Schema[..64];
            if (log.TableName?.Length > 128) log.TableName = log.TableName[..128];
            if (log.Action?.Length > 50) log.Action = log.Action[..50];
            if (log.PreviousHash?.Length > 128) log.PreviousHash = log.PreviousHash[..128];
            if (log.StoredHash?.Length > 128) log.StoredHash = log.StoredHash[..128];
            if (log.GapReason?.Length > 500) log.GapReason = log.GapReason[..500];
            if (log.OriginalStoredHash?.Length > 128) log.OriginalStoredHash = log.OriginalStoredHash[..128];
        }

        private static async Task FallbackSaveAsync(TContext dbContext, List<AuditLogEntity> batch, CancellationToken cancellationToken)
        {
            dbContext.ChangeTracker.Clear();

            // 若 Id <= 0 代表僅為記憶體暫存的臨時 Id，需重置為 0 以讓 EF Core 正確觸發 MSSQL IDENTITY 插入
            foreach (var log in batch)
            {
                if (log.Id <= 0)
                {
                    log.Id = 0;
                }
            }

            await dbContext.Set<AuditLogEntity>().AddRangeAsync(batch, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        private static string CalculateHash(AuditLogEntity log, string previousHash)
        {
            string rawData = $"{log.TableName}{log.Action}{log.KeyValues}{log.OldValues}{log.NewValues}{log.Timestamp:o}{previousHash}";
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
            return Convert.ToHexString(bytes);
        }
    }
}