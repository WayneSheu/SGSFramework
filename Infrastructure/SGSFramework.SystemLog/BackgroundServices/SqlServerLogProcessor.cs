#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace SGSFramework.SystemLog.BackgroundServices
{
    public interface ISqlServerLogProcessor
    {
        Task ProcessBatchAsync(IEnumerable<LogEvent> items, CancellationToken ct);
        Task FallbackAsync(IEnumerable<LogEvent> items, Exception ex);
    }

    public class SqlServerLogProcessor : ISqlServerLogProcessor
    {
        private readonly string _connectionString;
        private readonly ILogger<SqlServerLogProcessor> _logger;

        public SqlServerLogProcessor(IConfiguration configuration, ILogger<SqlServerLogProcessor> logger)
        {
            _connectionString = configuration.GetValue<string>("PersistentSettings:ConnectionStrings:DefaultConnection")
                ?? throw new InvalidOperationException("未配置 DefaultConnection 資料庫連線字串。");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ProcessBatchAsync(IEnumerable<LogEvent> items, CancellationToken ct)
        {
            var table = BuildDataTable(items);
            if (table.Rows.Count == 0) return;

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            using var bulkCopy = new SqlBulkCopy(connection)
            {
                DestinationTableName = "core.SystemLogs",
                BatchSize = table.Rows.Count,
                BulkCopyTimeout = 30
            };

            // 💡 顯式指定 Column Mapping，避免 Target Column 不符引發 InvalidOperationException
            ConfigureColumnMappings(bulkCopy);

            await bulkCopy.WriteToServerAsync(table, ct);
        }

        public Task FallbackAsync(IEnumerable<LogEvent> items, Exception ex)
        {
            // 降級處理邏輯（例如備份至本地 File System 或 Memory Queue）
            _logger.LogWarning(ex, "[Fallback] 日誌持久化降級，改為本地降級處理機制。");
            return Task.CompletedTask;
        }

        private static DataTable BuildDataTable(IEnumerable<LogEvent> items)
        {
            var dt = new DataTable();
            dt.Columns.Add("TimeStamp", typeof(DateTime));
            dt.Columns.Add("Message", typeof(string));
            dt.Columns.Add("Level", typeof(string));
            dt.Columns.Add("Exception", typeof(string));
            dt.Columns.Add("TenantId", typeof(string));
            dt.Columns.Add("UserId", typeof(string));
            dt.Columns.Add("ModuleName", typeof(string));
            dt.Columns.Add("Operation", typeof(string));
            dt.Columns.Add("CorrelationId", typeof(string));
            dt.Columns.Add("IP", typeof(string));
            dt.Columns.Add("Url", typeof(string));
            dt.Columns.Add("Payload", typeof(string));
            dt.Columns.Add("PrevHash", typeof(string));
            dt.Columns.Add("CurrentHash", typeof(string));
            dt.Columns.Add("AlertId", typeof(string));
            dt.Columns.Add("Fingerprint", typeof(string));

            foreach (var item in items)
            {
                var row = dt.NewRow();
                row["TimeStamp"] = item.Timestamp.DateTime;
                row["Message"] = item.RenderMessage();
                row["Level"] = item.Level.ToString();
                row["Exception"] = item.Exception?.ToString();

                // 映射 Serilog Properties 或自定義延伸屬性
                row["TenantId"] = item.Properties.TryGetValue("TenantId", out var tId) ? tId.ToString()?.Trim('"') : DBNull.Value;
                row["UserId"] = item.Properties.TryGetValue("UserId", out var uId) ? uId.ToString()?.Trim('"') : DBNull.Value;
                row["ModuleName"] = item.Properties.TryGetValue("ModuleName", out var mName) ? mName.ToString()?.Trim('"') : DBNull.Value;
                row["Operation"] = item.Properties.TryGetValue("Operation", out var op) ? op.ToString()?.Trim('"') : DBNull.Value;
                row["CorrelationId"] = item.Properties.TryGetValue("CorrelationId", out var cId) ? cId.ToString()?.Trim('"') : DBNull.Value;
                row["IP"] = item.Properties.TryGetValue("IP", out var ip) ? ip.ToString()?.Trim('"') : DBNull.Value;
                row["Url"] = item.Properties.TryGetValue("Url", out var url) ? url.ToString()?.Trim('"') : DBNull.Value;

                row["Payload"] = item.Properties.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(item.Properties) : string.Empty;

                // 雜湊鏈結與告警欄位預設值
                row["PrevHash"] = DBNull.Value;
                row["CurrentHash"] = DBNull.Value;
                row["AlertId"] = item.Properties.TryGetValue("AlertId", out var aId) ? aId.ToString()?.Trim('"') : DBNull.Value;
                row["Fingerprint"] = item.Properties.TryGetValue("Fingerprint", out var fp) ? fp.ToString()?.Trim('"') : DBNull.Value;

                dt.Rows.Add(row);
            }

            return dt;
        }
        private static void ConfigureColumnMappings(SqlBulkCopy bulkCopy)
        {
            bulkCopy.ColumnMappings.Clear();
            bulkCopy.ColumnMappings.Add("TimeStamp", "TimeStamp");
            bulkCopy.ColumnMappings.Add("Message", "Message");
            bulkCopy.ColumnMappings.Add("Level", "Level");
            bulkCopy.ColumnMappings.Add("Exception", "Exception");
            bulkCopy.ColumnMappings.Add("TenantId", "TenantId");
            bulkCopy.ColumnMappings.Add("UserId", "UserId");
            bulkCopy.ColumnMappings.Add("ModuleName", "ModuleName");
            bulkCopy.ColumnMappings.Add("Operation", "Operation");
            bulkCopy.ColumnMappings.Add("CorrelationId", "CorrelationId");
            bulkCopy.ColumnMappings.Add("IP", "IP");
            bulkCopy.ColumnMappings.Add("Url", "Url");
            bulkCopy.ColumnMappings.Add("Payload", "Payload");
            bulkCopy.ColumnMappings.Add("AlertId", "AlertId");
            bulkCopy.ColumnMappings.Add("Fingerprint", "Fingerprint");
        }

    }
}