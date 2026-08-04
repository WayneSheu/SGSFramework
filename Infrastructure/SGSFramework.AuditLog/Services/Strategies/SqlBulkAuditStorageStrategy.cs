using EFCore.BulkExtensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.AuditLogs;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using System;
using System.Collections.Generic;
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

            var bulkConfig = GetBulkConfig();

            try
            {
                await dbContext.BulkInsertAsync(batch, bulkConfig, cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is SqlException || ex is DbUpdateException)
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

            var bulkConfig = GetBulkConfig();

            try
            {
                await dbContext.BulkInsertAsync(fallbackLogs, bulkConfig, cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is SqlException || ex is DbUpdateException)
            {
                _logger?.LogWarning(ex, "[AuditLog Recover] Recover BulkInsert failed. Fallback to standard save.");
                await FallbackSaveAsync(dbContext, fallbackLogs, cancellationToken);
            }
        }

        private static BulkConfig GetBulkConfig()
        {
            return new BulkConfig
            {
                BatchSize = 2000,
                // 停用 PreserveInsertOrder，防止 BulkExtensions 使用 null 主鍵尋找字典
                PreserveInsertOrder = false,
                // 避開主鍵反寫 (不將寫入後的自增 Id 回傳覆蓋記憶體實體)
                SetOutputIdentity = false,
                // 忽略 MSSQL Ledger 陰影欄位 (如 ledger_start_transaction_id)
                EnableShadowProperties = false,
                UseTempDB = false
            };
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