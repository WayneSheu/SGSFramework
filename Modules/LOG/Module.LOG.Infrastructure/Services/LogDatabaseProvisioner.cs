using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGS.Modules.LOG.Infrastructure.Extensions;

namespace SGS.Modules.LOG.Infrastructure.Services;

/// <summary>
/// 獨立日誌庫的自動建置（於套用 Migration 前執行，部署 / 上傳模組後首次啟動即可使用）：
/// <list type="number">
/// <item>以部署帳號 (LogMigrationConnection) 連線 master，資料庫不存在時 CREATE DATABASE（建立者即為該庫 dbo）。</item>
/// <item>建立執行期帳號 (LogConnection) 與備份帳號 (BackupConnection) 的資料庫使用者；備份帳號加入 db_backupoperator，
/// 讓排程「日誌資料庫完整備份」可直接執行。</item>
/// <item>建立 log schema、授予執行期帳號 SELECT / INSERT 與 VIEW LEDGER CONTENT，並啟用快照隔離（Ledger 驗證需要）。</item>
/// </list>
/// 部署帳號需具 CREATE ANY DATABASE（docs/sql/20261105_grant_log_database_autocreate.sql）；
/// 未授予時改由 DBA 執行 docs/sql/20261105_create_log_database.sql 建庫，本類別只補建缺少的使用者與權限（無權限時僅警告）。
/// 所有名稱一律以參數傳入並於 T-SQL 內 QUOTENAME 組合，避免注入。
/// </summary>
public sealed class LogDatabaseProvisioner(IConfiguration configuration, ILogger<LogDatabaseProvisioner> logger)
{
    public async Task EnsureProvisionedAsync(CancellationToken cancellationToken = default)
    {
        var migration = configuration[LogDatabaseOptions.MigrationConnectionKey];
        var runtime = configuration[LogDatabaseOptions.RuntimeConnectionKey];
        if (string.IsNullOrWhiteSpace(migration))
        {
            logger.LogWarning("[LOG 模組] 未設定 {Key}，改用執行期帳號建置日誌庫（建議設定獨立的部署帳號）。", LogDatabaseOptions.MigrationConnectionKey);
            migration = runtime;
        }

        if (string.IsNullOrWhiteSpace(migration)) return;

        var builder = new SqlConnectionStringBuilder(migration);
        var database = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new InvalidOperationException($"日誌庫連線字串未指定資料庫（{LogDatabaseOptions.MigrationConnectionKey}）。");
        }

        var created = await EnsureDatabaseAsync(new SqlConnectionStringBuilder(migration) { InitialCatalog = "master" }.ConnectionString, database, cancellationToken)
            .ConfigureAwait(false);

        await using var connection = new SqlConnection(migration);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await TryAsync(connection, "建立 log schema",
            "IF SCHEMA_ID(N'log') IS NULL EXEC (N'CREATE SCHEMA [log] AUTHORIZATION [dbo]');", cancellationToken).ConfigureAwait(false);

        var runtimeUser = UserOf(runtime);
        if (runtimeUser is not null)
        {
            await TryAsync(connection, $"建立執行期帳號 {runtimeUser} 的資料庫使用者並授權",
                """
                IF DATABASE_PRINCIPAL_ID(@user) IS NULL
                BEGIN
                    DECLARE @create nvarchar(400) = N'CREATE USER ' + QUOTENAME(@user) + N' FOR LOGIN ' + QUOTENAME(@user) + N';';
                    EXEC (@create);
                END
                DECLARE @grant nvarchar(400) = N'GRANT SELECT, INSERT ON SCHEMA::[log] TO ' + QUOTENAME(@user) + N'; GRANT VIEW LEDGER CONTENT TO ' + QUOTENAME(@user) + N';';
                EXEC (@grant);
                """, cancellationToken, ("@user", runtimeUser)).ConfigureAwait(false);
        }

        var backupUser = UserOf(configuration["PersistentSettings:ConnectionStrings:BackupConnection"]);
        if (backupUser is not null)
        {
            await TryAsync(connection, $"建立備份帳號 {backupUser} 的資料庫使用者（db_backupoperator）",
                """
                IF DATABASE_PRINCIPAL_ID(@user) IS NULL
                BEGIN
                    DECLARE @create nvarchar(400) = N'CREATE USER ' + QUOTENAME(@user) + N' FOR LOGIN ' + QUOTENAME(@user) + N';';
                    EXEC (@create);
                END
                IF IS_ROLEMEMBER(N'db_backupoperator', @user) = 0
                BEGIN
                    DECLARE @role nvarchar(400) = N'ALTER ROLE [db_backupoperator] ADD MEMBER ' + QUOTENAME(@user) + N';';
                    EXEC (@role);
                END
                """, cancellationToken, ("@user", backupUser)).ConfigureAwait(false);
        }

        await TryAsync(connection, "啟用快照隔離（Ledger 驗證需要）",
            """
            IF (SELECT snapshot_isolation_state FROM sys.databases WHERE name = DB_NAME()) = 0
            BEGIN
                DECLARE @alter nvarchar(400) = N'ALTER DATABASE ' + QUOTENAME(DB_NAME()) + N' SET ALLOW_SNAPSHOT_ISOLATION ON;';
                EXEC (@alter);
            END
            """, cancellationToken).ConfigureAwait(false);

        if (created)
        {
            logger.LogInformation("[LOG 模組] 已自動建立日誌資料庫 {Database} 與帳號權限。", database);
        }
    }

    /// <summary>資料庫不存在時建立；回傳是否為本次建立</summary>
    private async Task<bool> EnsureDatabaseAsync(string masterConnection, string database, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(masterConnection);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            IF DB_ID(@db) IS NOT NULL
                SELECT CAST(0 AS bit);
            ELSE
            BEGIN
                DECLARE @create nvarchar(400) = N'CREATE DATABASE ' + QUOTENAME(@db) + N';';
                EXEC (@create);
                SELECT CAST(1 AS bit);
            END
            """;
        command.Parameters.Add(new SqlParameter("@db", System.Data.SqlDbType.NVarChar, 128) { Value = database });
        command.CommandTimeout = 300;

        try
        {
            var created = (bool)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? false);
            if (created) logger.LogInformation("[LOG 模組] 日誌資料庫 {Database} 不存在，已以部署帳號建立。", database);
            return created;
        }
        catch (SqlException ex) when (ex.Number is 262 or 229 or 1802)
        {
            throw new InvalidOperationException(
                $"日誌資料庫 {database} 不存在，且部署帳號無建立資料庫權限（SQL 錯誤 {ex.Number}）。" +
                "請由 DBA 執行 docs/sql/20261105_grant_log_database_autocreate.sql 授權自動建庫，" +
                "或執行 docs/sql/20261105_create_log_database.sql 手動建庫後重新啟動 API。", ex);
        }
    }

    private async Task TryAsync(SqlConnection connection, string step, string sql, CancellationToken cancellationToken, params (string Name, string Value)[] parameters)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                command.Parameters.Add(new SqlParameter(name, System.Data.SqlDbType.NVarChar, 128) { Value = value });
            }
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException ex)
        {
            // 既有資料庫由 DBA 建立時，部署帳號可能無權管理使用者 / 資料庫設定：僅警告，由 DBA 腳本補齊
            logger.LogWarning(ex, "[LOG 模組] {Step}失敗（SQL 錯誤 {Number}），請由 DBA 執行 docs/sql/20261105_create_log_database.sql。", step, ex.Number);
        }
    }

    private static string? UserOf(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return null;
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return builder.IntegratedSecurity || string.IsNullOrWhiteSpace(builder.UserID) ? null : builder.UserID;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
