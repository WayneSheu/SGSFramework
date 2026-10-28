using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.Core.Configurations;
using SGSFramework.Scheduler.Options;

namespace SGSFramework.Scheduler.Maintenance;

/// <summary>資料庫備份結果</summary>
public sealed record DatabaseBackupResult(
    string Database,
    string BackupType,
    string FilePath,
    bool Verified,
    long? SizeBytes,
    int ExpiredFilesDeleted,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);

/// <summary>備份無法執行（設定錯誤、權限不足、SQL 錯誤），屬可預期的業務失敗</summary>
public sealed class DatabaseBackupException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>
/// SQL Server 資料庫備份 (BACKUP DATABASE)。備份檔寫在 SQL Server 主機端，
/// 執行帳號需具 db_backupoperator（見 PersistentSettings:ConnectionStrings:BackupConnection）。
/// </summary>
public interface IDatabaseBackupService
{
    Task<DatabaseBackupResult> BackupAsync(bool differential, CancellationToken cancellationToken = default);
}

public sealed partial class SqlServerDatabaseBackupService(
    IOptions<PersistentConnectionOptions> connectionOptions,
    IOptionsMonitor<SchedulerOptions> schedulerOptions,
    TimeProvider timeProvider,
    ILogger<SqlServerDatabaseBackupService> logger) : IDatabaseBackupService
{
    // 同一節點同時只允許一個備份作業（跨節點由 Hangfire DisableConcurrentExecution 分散式鎖保護）
    private static readonly SemaphoreSlim BackupGate = new(1, 1);

    public async Task<DatabaseBackupResult> BackupAsync(bool differential, CancellationToken cancellationToken = default)
    {
        if (!await BackupGate.WaitAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false))
        {
            throw new DatabaseBackupException("已有資料庫備份作業執行中，請稍後再試。");
        }

        try
        {
            return await BackupCoreAsync(differential, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            BackupGate.Release();
        }
    }

    private async Task<DatabaseBackupResult> BackupCoreAsync(bool differential, CancellationToken cancellationToken)
    {
        var options = schedulerOptions.CurrentValue.DatabaseBackup;
        var connectionString = ResolveConnectionString();
        var database = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        if (string.IsNullOrWhiteSpace(database))
        {
            throw new DatabaseBackupException("備份連線字串未指定資料庫 (Database / Initial Catalog)。");
        }

        if (differential && options.CopyOnly)
        {
            throw new DatabaseBackupException("差異備份不支援 COPY_ONLY，請關閉 Scheduler:DatabaseBackup:CopyOnly 或改用完整備份。");
        }

        var startedAt = timeProvider.GetUtcNow();
        var backupType = differential ? "DIFF" : "FULL";

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var directory = string.IsNullOrWhiteSpace(options.Directory)
                ? await GetInstanceDefaultBackupPathAsync(connection, cancellationToken).ConfigureAwait(false)
                : options.Directory.Trim();

            var fileName = $"{SafeFilePart(database)}_{backupType}_{startedAt:yyyyMMdd_HHmmss}Z.bak";
            var filePath = CombineServerPath(directory, fileName);

            logger.LogInformation("[DatabaseBackup] 開始{Type}備份 {Database} → {Path}", differential ? "差異" : "完整", database, filePath);

            var withOptions = new List<string> { "INIT", "FORMAT", "STATS = 10" };
            if (differential) withOptions.Add("DIFFERENTIAL");
            if (options.CopyOnly) withOptions.Add("COPY_ONLY");
            if (options.Compression) withOptions.Add("COMPRESSION");
            if (options.Checksum) withOptions.Add("CHECKSUM");

            // 資料庫名稱與路徑一律以參數傳遞，避免 SQL Injection
            await using (var backup = connection.CreateCommand())
            {
                backup.CommandText = $"BACKUP DATABASE @database TO DISK = @path WITH {string.Join(", ", withOptions)}, NAME = @name;";
                backup.CommandTimeout = options.CommandTimeoutSeconds;
                backup.Parameters.Add(new SqlParameter("@database", SqlDbType.NVarChar, 128) { Value = database });
                backup.Parameters.Add(new SqlParameter("@path", SqlDbType.NVarChar, 4000) { Value = filePath });
                backup.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 128) { Value = $"{database} {backupType} {startedAt:yyyy-MM-dd HH:mm:ss}Z" });
                await backup.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            bool verified = false;
            if (options.Verify)
            {
                await using var verify = connection.CreateCommand();
                verify.CommandText = options.Checksum
                    ? "RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM;"
                    : "RESTORE VERIFYONLY FROM DISK = @path;";
                verify.CommandTimeout = options.CommandTimeoutSeconds;
                verify.Parameters.Add(new SqlParameter("@path", SqlDbType.NVarChar, 4000) { Value = filePath });
                try
                {
                    await verify.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (SqlException ex)
                {
                    // 備份檔已產生，僅驗證失敗：明確區分，避免誤以為沒有備份（重試也不會改善權限問題）
                    logger.LogWarning(ex, "[DatabaseBackup] {Database} {Type} 備份檔已產生，但驗證失敗 (SQL 錯誤 {Number})：{Path}",
                        database, backupType, ex.Number, filePath);
                    var reason = ex.Number is 262 or 229
                        ? "備份帳號缺少 master 的 CREATE DATABASE 權限（RESTORE VERIFYONLY 需要），請執行授權腳本，或設定 Scheduler:DatabaseBackup:Verify = false。"
                        : ex.Message;
                    throw new DatabaseBackupException($"備份檔已產生（{filePath}），但驗證失敗：{reason}", ex);
                }
                verified = true;
            }

            long? size = TryGetFileSize(filePath);
            int deleted = options.RetentionDays > 0
                ? DeleteExpiredBackups(directory, database, options.RetentionDays, filePath)
                : 0;

            var completedAt = timeProvider.GetUtcNow();
            logger.LogInformation(
                "[DatabaseBackup] {Database} {Type} 備份完成，耗時 {Elapsed}，驗證：{Verified}，清除過期備份 {Deleted} 個。",
                database, backupType, completedAt - startedAt, verified, deleted);

            return new DatabaseBackupResult(database, backupType, filePath, verified, size, deleted, startedAt, completedAt);
        }
        catch (SqlException ex)
        {
            // Warning 而非 Error：Email 告警由排程執行紀錄於作業「最終失敗」時統一發出，避免每次嘗試都寄信
            logger.LogWarning(ex, "[DatabaseBackup] {Database} {Type} 備份失敗 (SQL 錯誤 {Number})。", database, backupType, ex.Number);
            var hint = ex.Number switch
            {
                262 or 229 or 3110 => "備份帳號權限不足，請授予 db_backupoperator（PersistentSettings:ConnectionStrings:BackupConnection）。",
                18456 => "備份帳號無法登入，請確認登入帳號已建立、已啟用，且密碼與 BackupConnection 一致。",
                _ => ex.Message
            };
            throw new DatabaseBackupException($"資料庫備份失敗：{hint}", ex);
        }
    }

    private string ResolveConnectionString()
    {
        var connections = connectionOptions.Value;
        if (!string.IsNullOrWhiteSpace(connections.BackupConnection)) return connections.BackupConnection;

        logger.LogWarning("[DatabaseBackup] 未設定 BackupConnection，改用 {Fallback}；建議建立僅具 db_backupoperator 的專用備份帳號。",
            string.IsNullOrWhiteSpace(connections.MigrationConnection) ? "DefaultConnection" : "MigrationConnection");
        return string.IsNullOrWhiteSpace(connections.MigrationConnection)
            ? connections.DefaultConnection
            : connections.MigrationConnection;
    }

    private static async Task<string> GetInstanceDefaultBackupPathAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000));";
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
        return string.IsNullOrWhiteSpace(value)
            ? throw new DatabaseBackupException("無法取得 SQL Server 預設備份目錄，請設定 Scheduler:DatabaseBackup:Directory。")
            : value;
    }

    /// <summary>依 SQL Server 主機的路徑格式組合（主機可能為 Linux，不能使用本機的 Path.Combine）</summary>
    private static string CombineServerPath(string directory, string fileName)
    {
        char separator = directory.Contains('\\', StringComparison.Ordinal) || !directory.Contains('/', StringComparison.Ordinal) ? '\\' : '/';
        return directory.TrimEnd('\\', '/') + separator + fileName;
    }

    private static string SafeFilePart(string value) => InvalidFileChars().Replace(value, "_");

    private long? TryGetFileSize(string filePath)
    {
        try
        {
            var file = new FileInfo(filePath);
            return file.Exists ? file.Length : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            logger.LogDebug(ex, "[DatabaseBackup] 應用程式主機無法存取備份檔 {Path}，略過檔案大小。", filePath);
            return null;
        }
    }

    /// <summary>
    /// 刪除本系統產生（檔名符合 {資料庫}_{FULL|DIFF}_yyyyMMdd_HHmmssZ.bak）且超過保留天數的備份檔；
    /// 其他檔案（DBA 手動備份等）一律不動，且永遠保留本次剛產生的檔案。
    /// </summary>
    private int DeleteExpiredBackups(string directory, string database, int retentionDays, string currentFile)
    {
        try
        {
            if (!System.IO.Directory.Exists(directory))
            {
                logger.LogInformation("[DatabaseBackup] 應用程式主機無法存取備份目錄 {Directory}（可能位於 SQL Server 主機本機），略過過期備份清理。", directory);
                return 0;
            }

            var pattern = new Regex($"^{Regex.Escape(SafeFilePart(database))}_(FULL|DIFF)_\\d{{8}}_\\d{{6}}Z\\.bak$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var threshold = timeProvider.GetUtcNow().AddDays(-retentionDays).UtcDateTime;
            int deleted = 0;

            foreach (var file in new DirectoryInfo(directory).EnumerateFiles("*.bak"))
            {
                if (!pattern.IsMatch(file.Name) || file.LastWriteTimeUtc >= threshold) continue;
                if (string.Equals(file.FullName, currentFile, StringComparison.OrdinalIgnoreCase)) continue;

                file.Delete();
                deleted++;
                logger.LogInformation("[DatabaseBackup] 已刪除過期備份 {File}（保留 {Days} 天）。", file.Name, retentionDays);
            }

            return deleted;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 清理失敗不影響本次備份結果
            logger.LogWarning(ex, "[DatabaseBackup] 清理過期備份失敗：{Directory}", directory);
            return 0;
        }
    }

    [GeneratedRegex(@"[^\w\-]", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidFileChars();
}
