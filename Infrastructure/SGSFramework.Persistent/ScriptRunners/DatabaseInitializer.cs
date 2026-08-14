using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGSFramework.Persistent.Abstractions.ScriptRunners;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace SGSFramework.Persistent.ScriptRunners
{
    public class DatabaseInitializer : IDatabaseInitializer
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DatabaseInitializer> _logger;

        public DatabaseInitializer(IConfiguration configuration, ILogger<DatabaseInitializer> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
        {
            var migrationConnectionString = _configuration.GetSection("PersistentSettings:ConnectionStrings")["BootstrappingConnection"];

            if (string.IsNullOrWhiteSpace(migrationConnectionString))
            {
                _logger.LogWarning("未設定 MigrationConnection，跳過資料庫初始化腳本執行。");
                return;
            }

            // 確保連線字串明確指定安全參數，防止 TLS 握手階段被伺服器斷開
            var connectionBuilder = new SqlConnectionStringBuilder(migrationConnectionString);

            // 開發環境保險機制：若無明確設定，預設為對證書信任以避免 Error 233
            if (!connectionBuilder.ContainsKey("TrustServerCertificate"))
            {
                connectionBuilder.TrustServerCertificate = true;
            }

            try
            {
                await using var connection = new SqlConnection(connectionBuilder.ConnectionString);
                _logger.LogInformation("嘗試連線至資料庫進行初始化操作... Server: {DataSource}, Database: {InitialCatalog}",
                    connectionBuilder.DataSource, connectionBuilder.InitialCatalog);

                await connection.OpenAsync(cancellationToken);
                _logger.LogInformation("資料庫連線建立成功，開始掃描並執行初始化腳本。");

                var scriptFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Scripts");
                if (!Directory.Exists(scriptFolderPath))
                {
                    _logger.LogWarning("未找到 SQL 腳本目錄: {Path}", scriptFolderPath);
                    return;
                }

                var sqlFiles = Directory.GetFiles(scriptFolderPath, "*.sql").OrderBy(f => f).ToList();
                foreach (var file in sqlFiles)
                {
                    await ExecuteSqlScriptAsync(connection, file, cancellationToken);
                }
            }
            catch (SqlException ex) when (ex.Number == 233)
            {
                _logger.LogCritical(ex, "SQL Server 拒絕登入連線 (Error 233)。請檢查：1. MSSQL 是否已啟用 TCP/IP 通訊協定。 2. 是否開啟 SQL Server 混合驗證模式。 3. deploy_sgs_user 帳號狀態。");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "資料庫初始化腳本執行過程發生未預期例外。");
                throw;
            }
        }

        private async Task ExecuteSqlScriptAsync(SqlConnection connection, string filePath, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(connection);
            ArgumentException.ThrowIfNullOrEmpty(filePath);

            var fileName = Path.GetFileName(filePath);
            _logger.LogInformation("開始執行 SQL 檔案: {FileName}", fileName);

            var scriptContent = await File.ReadAllTextAsync(filePath, cancellationToken);

            // 解析 T-SQL 的 GO 關鍵字 (批次分隔)
            var batches = Regex.Split(
                scriptContent,
                @"^\s*GO\s*$",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);

            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;

                await using var command = connection.CreateCommand();
                command.CommandText = batch;
                command.CommandTimeout = 180; // 初始化腳本逾時時間

                try
                {
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                catch (SqlException ex)
                {
                    _logger.LogError(ex, "執行 SQL 批次失敗。檔名: {FileName}, 指令內容: {Batch}", fileName, batch);
                    throw;
                }
            }

            _logger.LogInformation("SQL 檔案 {FileName} 執行完成。", fileName);
        }
    }
}
