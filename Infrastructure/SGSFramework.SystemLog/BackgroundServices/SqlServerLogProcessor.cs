#nullable enable
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Serilog.Events;
using SGSFramework.Core.Abstractions.Processors;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.SystemLog.BackgroundServices
{
    public class SqlServerLogProcessor : IPersistentProcessor<LogEvent>
    {
        private readonly string _connectionString;
        private readonly string _fallbackPath;
        private readonly string _tableName = "SystemLogs";

        public SqlServerLogProcessor(IConfiguration config)
        {
            ArgumentNullException.ThrowIfNull(config);

            _connectionString = config.GetValue<string>("PersistentOptions:DatabaseSettings:ConnectionString")
                ?? throw new InvalidOperationException("找不到PersistentOptions:DatabaseSettings:ConnectionString連線字串。");

            _fallbackPath = config["Logging:FallbackPath"] ?? @"C:\Logs\Fallback\";
            if (!Directory.Exists(_fallbackPath)) Directory.CreateDirectory(_fallbackPath);
        }

        public async Task ProcessBatchAsync(IEnumerable<LogEvent> items, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(items);

            using var dt = CreateDataTable(items);
            using var bulkCopy = new SqlBulkCopy(_connectionString, SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.UseInternalTransaction);

            bulkCopy.DestinationTableName = _tableName;
            bulkCopy.BatchSize = 1000;

            foreach (DataColumn column in dt.Columns)
            {
                bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            }

            await bulkCopy.WriteToServerAsync(dt, ct);
        }

        public async Task FallbackAsync(IEnumerable<LogEvent> items, Exception ex)
        {
            var fileName = Path.Combine(_fallbackPath, $"fallback-{DateTime.Now:yyyyMMdd}.log");
            var content = items.Select(i => $"[{i.Timestamp:O}] [{i.Level}] {i.RenderMessage()} | EX: {ex.Message}");

            await File.AppendAllLinesAsync(fileName, content);
        }

        private DataTable CreateDataTable(IEnumerable<LogEvent> items)
        {
            var dt = new DataTable();

            // 1. 基礎日誌欄位
            dt.Columns.Add("TimeStamp", typeof(DateTime));
            dt.Columns.Add("Level", typeof(string));
            dt.Columns.Add("Message", typeof(string));
            dt.Columns.Add("Payload", typeof(string));
            dt.Columns.Add("Exception", typeof(string));

            // 2. 核心追蹤與資安防禦特徵欄位 (💡 新增)
            dt.Columns.Add("AlertId", typeof(string));
            dt.Columns.Add("Fingerprint", typeof(string));

            // 3. 既有業務上下文欄位
            dt.Columns.Add("CorrelationId", typeof(string));
            dt.Columns.Add("TenantId", typeof(string));
            dt.Columns.Add("UserId", typeof(string));
            dt.Columns.Add("ModuleName", typeof(string));
            dt.Columns.Add("Operation", typeof(string));
            dt.Columns.Add("IP", typeof(string));
            dt.Columns.Add("Url", typeof(string));
            dt.Columns.Add("PrevHash", typeof(string));
            dt.Columns.Add("CurrentHash", typeof(string));

            foreach (var item in items)
            {
                string? GetProp(string name) =>
                    item.Properties.TryGetValue(name, out var val) ? val.ToString().Trim('"') : null;

                dt.Rows.Add(
                    item.Timestamp.DateTime,
                    item.Level.ToString(),
                    item.RenderMessage(),
                    // 所有原始屬性序列化保留於 Payload (包含 AlertId 與 Fingerprint 複本)
                    item.Properties.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(item.Properties.ToDictionary(k => k.Key, v => v.Value.ToString().Trim('"'))) : null,
                    item.Exception?.ToString(),

                    // 提取獨立核心欄位值
                    GetProp("AlertId"),
                    GetProp("Fingerprint"),

                    // 業務欄位
                    GetProp("CorrelationId"),
                    GetProp("TenantId"),
                    GetProp("UserId"),
                    GetProp("ModuleName"),
                    GetProp("Operation"),
                    GetProp("IP"),
                    GetProp("Url"),
                    GetProp("PrevHash"),
                    GetProp("CurrentHash")
                );
            }

            return dt;
        }
    }
}