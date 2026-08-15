#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Serilog.Events;
using SGSFramework.Core.Abstractions.Processors;
using SGSFramework.SystemLog.Readers;

namespace SGSFramework.SystemLog.BackgroundServices;

/// <summary>
/// Infrastructure 層：負責將 Serilog 事件批次寫入 MSSQL 資料庫 (核心系統日誌)
/// </summary>
public sealed class SqlServerLogProcessor : IPersistentProcessor<LogEvent>
{
    private readonly string _connectionString;
    private readonly string _fallbackPath;
    private readonly string _tableName = "core.SystemLogs";

    public SqlServerLogProcessor(IConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        _connectionString = config.GetSection("PersistentSettings:ConnectionStrings")["DefaultConnection"]
            ?? config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("找不到 PersistentSettings:ConnectionStrings:DefaultConnection 連線字串。");

        _fallbackPath = config["Logging:FallbackPath"] ?? @"C:\Logs\Fallback\";
        if (!Directory.Exists(_fallbackPath))
        {
            Directory.CreateDirectory(_fallbackPath);
        }
    }

    public async Task ProcessBatchAsync(IEnumerable<LogEvent> items, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(items);

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct).ConfigureAwait(false);

            using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock | SqlBulkCopyOptions.UseInternalTransaction, null)
            {
                DestinationTableName = _tableName,
                BatchSize = 1000,
                BulkCopyTimeout = 30
            };

            using var reader = new LogEventDataReader(items);

            // 動態映射 LogEventDataReader 所提供的所有欄位名稱（包含可修復 CS0535/ Error 515 的 CreatedAt 欄位）
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var colName = reader.GetName(i);
                bulkCopy.ColumnMappings.Add(colName, colName);
            }

            await bulkCopy.WriteToServerAsync(reader, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await FallbackAsync(items, ex).ConfigureAwait(false);
            throw;
        }
    }

    public async Task FallbackAsync(IEnumerable<LogEvent> items, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(ex);

        try
        {
            var fileName = Path.Combine(_fallbackPath, $"fallback-{DateTime.UtcNow:yyyyMMdd}.log");
            var content = items.Select(i => $"[{i.Timestamp:O}] [{i.Level}] {i.RenderMessage()} | EX: {ex.Message}");

            await File.AppendAllLinesAsync(fileName, content, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // 防止 Fallback 內部二次拋出例外蓋掉原始 SqlException
        }
    }
}