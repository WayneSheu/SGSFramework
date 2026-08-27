// Path: src/SGSFramework/Infrastructure/SGSFramework.Persistent/ScriptRunners/Strategies/SqlScriptExecutionStrategy.cs
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Database;
using System.Data;
using System.Text.RegularExpressions;

namespace SGSFramework.Persistent.ScriptRunners.Strategies;

// 關鍵修復：類別必須宣告為 partial，Source Generator 才能注入生成的實作
public sealed partial class SqlScriptExecutionStrategy : IScriptExecutionStrategy
{
    private readonly ILogger<SqlScriptExecutionStrategy> _logger;
    private const int MaxRetryCount = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    // C# 10+ / .NET 7+ 增強型 Source Generator Regex
    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex GoBatchRegex();

    public SqlScriptExecutionStrategy(ILogger<SqlScriptExecutionStrategy> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteScriptAsync(string connectionString, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionString);
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"找不到 SQL 初始化腳本檔案: {filePath}");
        }

        string fileName = Path.GetFileName(filePath);
        _logger.LogInformation("開始解析 SQL 檔案: {FileName}", fileName);

        string scriptContent = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        string[] batches = GoBatchRegex().Split(scriptContent);

        for (int attempt = 1; attempt <= MaxRetryCount; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                // 預防 Error 904: 檢查目標資料庫是否處於 ONLINE 狀態
                await ValidateDatabaseStateAsync(connection, cancellationToken).ConfigureAwait(false);

                foreach (string batch in batches)
                {
                    if (string.IsNullOrWhiteSpace(batch))
                    {
                        continue;
                    }

                    await using var command = connection.CreateCommand();
                    command.CommandText = batch;
                    command.CommandTimeout = 180;
                    command.CommandType = CommandType.Text;

                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                _logger.LogInformation("SQL 檔案 {FileName} 執行完成。", fileName);
                return;
            }
            catch (SqlException ex) when (IsTransientError(ex) && attempt < MaxRetryCount)
            {
                _logger.LogWarning(ex, "執行 SQL 檔案 {FileName} 時發生暫時性錯誤 (Error: {ErrorNumber})。將於 {Delay} 秒後進行第 {NextAttempt} 次重試...",
                    fileName, ex.Number, RetryDelay.TotalSeconds, attempt + 1);

                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (SqlException ex)
            {
                _logger.LogError(ex, "執行 SQL 批次失敗。檔名: {FileName}", fileName);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "執行 SQL 檔案過程發生非預期例外。檔名: {FileName}", fileName);
                throw;
            }
        }
    }

    private static async Task ValidateDatabaseStateAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        const string query = @"
            SELECT state_desc 
            FROM sys.databases 
            WHERE name = DB_NAME();";

        await using var command = new SqlCommand(query, connection);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        if (result is string state && !state.Equals("ONLINE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"目標資料庫狀態異常 ({state})，無法執行初始化腳本。");
        }
    }

    private static bool IsTransientError(SqlException ex)
    {
        // 904: Database cannot be autostarted / Pending Recovery
        // 233: Connection failed / Shared Memory Provider
        // 40613: Database unavailable
        // 1205: Deadlock
        // -2: Timeout
        int[] transientCodes = [904, 233, 40613, 1205, -2];
        return transientCodes.Contains(ex.Number);
    }
}