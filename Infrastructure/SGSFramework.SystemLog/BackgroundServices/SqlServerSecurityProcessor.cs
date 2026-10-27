#nullable enable
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SGSFramework.Core.Configurations;
using Serilog.Events;
using SGSFramework.Core.Abstractions.Processors;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.SystemLog.BackgroundServices
{
    public class SqlServerSecurityProcessor : IPersistentProcessor<LogEvent>
    {
        private readonly string _connectionString;

        public SqlServerSecurityProcessor(IOptions<PersistentConnectionOptions> connectionOptions)
        {
            ArgumentNullException.ThrowIfNull(connectionOptions);
            _connectionString = connectionOptions.Value.DefaultConnection;
        }

        public async Task ProcessBatchAsync(IEnumerable<LogEvent> items, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(items);

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            using var transaction = connection.BeginTransaction();

            try
            {
                //完整對齊基底結構，加入實體欄位映射與新設查察欄位
                var sql = @"INSERT INTO core.SecurityLog 
                        (TimeStamp, Message, Level, Exception, LogType, EventCategory, UserId, ClientIp, CorrelationId, AlertId, Fingerprint) 
                        VALUES 
                        (@TimeStamp, @Message, @Level, @Exception, @LogType, @EventCategory, @UserId, @ClientIp, @CorrelationId, @AlertId, @Fingerprint);";

                foreach (var log in items)
                {
                    if (ct.IsCancellationRequested) break;

                    using var command = new SqlCommand(sql, connection, transaction);

                    // 基礎日誌本體
                    // 必須以 DateTimeOffset 寫入：若用 .DateTime 會丟掉時區偏移，本地時間被當成 UTC 存入，
                    // 讀出後前端 ToLocalTime() 會多加一次時差（台灣 +8 小時）
                    command.Parameters.Add("@TimeStamp", System.Data.SqlDbType.DateTimeOffset).Value = log.Timestamp;
                    command.Parameters.AddWithValue("@Message", log.RenderMessage());
                    command.Parameters.AddWithValue("@Level", log.Level.ToString());
                    command.Parameters.AddWithValue("@Exception", log.Exception?.ToString() ?? (object)DBNull.Value);

                    // 稽核環境變數
                    command.Parameters.AddWithValue("@LogType", GetProperty(log, "LogType"));
                    command.Parameters.AddWithValue("@EventCategory", GetProperty(log, "EventCategory"));
                    command.Parameters.AddWithValue("@UserId", GetProperty(log, "UserId"));
                    command.Parameters.AddWithValue("@ClientIp", GetProperty(log, "ClientIp"));

                    // 💡 新增擴充：高效查察與追蹤一等公民欄位
                    command.Parameters.AddWithValue("@CorrelationId", GetProperty(log, "CorrelationId"));
                    command.Parameters.AddWithValue("@AlertId", GetProperty(log, "AlertId"));
                    command.Parameters.AddWithValue("@Fingerprint", GetProperty(log, "Fingerprint"));

                    await command.ExecuteNonQueryAsync(ct);
                }

                await transaction.CommitAsync(ct);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }

        public async Task FallbackAsync(IEnumerable<LogEvent> items, Exception ex)
        {
            try
            {
                var fallbackPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "security_fallback.log");
                foreach (var log in items)
                {
                    await File.AppendAllTextAsync(fallbackPath, $"[{log.Timestamp}] {log.Level}: {log.RenderMessage()} | Ex: {ex.Message}\n");
                }
            }
            catch (Exception fallbackEx)
            {
                Serilog.Debugging.SelfLog.WriteLine($"Critical failure in Security Fallback: {fallbackEx.Message}");
            }
        }

        private static object GetProperty(LogEvent log, string propertyName)
        {
            if (log.Properties.TryGetValue(propertyName, out var value) && value is ScalarValue scalar)
            {
                return scalar.Value ?? DBNull.Value;
            }
            return DBNull.Value;
        }
    }
}