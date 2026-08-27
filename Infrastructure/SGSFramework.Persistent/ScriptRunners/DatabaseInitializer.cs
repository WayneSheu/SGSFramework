// Path: src/SGSFramework/Infrastructure/SGSFramework.Persistent/ScriptRunners/DatabaseInitializer.cs
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Database;

namespace SGSFramework.Persistent.ScriptRunners;

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly IScriptExecutionStrategy _executionStrategy;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IScriptExecutionStrategy executionStrategy,
        IConfiguration configuration,
        ILogger<DatabaseInitializer> logger)
    {
        _executionStrategy = executionStrategy ?? throw new ArgumentNullException(nameof(executionStrategy));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
    {
        string? migrationConnectionString = _configuration.GetSection("PersistentSettings:ConnectionStrings")["BootstrappingConnection"];

        if (string.IsNullOrWhiteSpace(migrationConnectionString))
        {
            _logger.LogWarning("未設定 BootstrappingConnection，跳過資料庫初始化腳本執行。");
            return;
        }

        var connectionBuilder = new SqlConnectionStringBuilder(migrationConnectionString);

        if (!connectionBuilder.ContainsKey("TrustServerCertificate"))
        {
            connectionBuilder.TrustServerCertificate = true;
        }

        string targetConnectionString = connectionBuilder.ConnectionString;

        try
        {
            _logger.LogInformation("嘗試連線至資料庫進行初始化操作... Server: {DataSource}, Database: {InitialCatalog}",
                connectionBuilder.DataSource, connectionBuilder.InitialCatalog);

            string scriptFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Scripts");
            if (!Directory.Exists(scriptFolderPath))
            {
                _logger.LogWarning("未找到 SQL 腳本目錄: {Path}", scriptFolderPath);
                return;
            }

            List<string> sqlFiles = Directory.GetFiles(scriptFolderPath, "*.sql")
                .OrderBy(f => f)
                .ToList();

            _logger.LogInformation("找到 {Count} 個初始化腳本，開始順序執行...", sqlFiles.Count);

            foreach (string file in sqlFiles)
            {
                await _executionStrategy.ExecuteScriptAsync(targetConnectionString, file, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("所有資料庫初始化腳本執行完畢。");
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
}