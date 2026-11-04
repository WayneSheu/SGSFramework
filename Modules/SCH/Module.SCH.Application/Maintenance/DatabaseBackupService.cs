using System.Collections.Concurrent;
using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.Core.Configurations;
using SGSFramework.Core.Scheduling;
using SGS.Modules.SCH.Application.Options;

namespace SGS.Modules.SCH.Application.Maintenance;

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
    /// <param name="differential">差異備份</param>
    /// <param name="target">外掛模組備份目標 Key（<see cref="ModuleDatabaseBackupTarget"/>）；null 表示主資料庫</param>
    Task<DatabaseBackupResult> BackupAsync(bool differential, string? target = null, CancellationToken cancellationToken = default);

    /// <summary>已宣告的外掛模組備份目標</summary>
    IReadOnlyList<ModuleDatabaseBackupTarget> Targets { get; }
}

public sealed partial class SqlServerDatabaseBackupService(
    IOptions<PersistentConnectionOptions> connectionOptions,
    IOptionsMonitor<SchedulerOptions> schedulerOptions,
    IConfiguration configuration,
    IEnumerable<ModuleDatabaseBackupTarget> backupTargets,
    TimeProvider timeProvider,
    ILogger<SqlServerDatabaseBackupService> logger) : IDatabaseBackupService
{
    // 同一節點同一資料庫同時只允許一個備份（不同資料庫可並行；跨節點由排程作業的分散式鎖保護）
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> BackupGates = new(StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyList<ModuleDatabaseBackupTarget> _targets =
        backupTargets.Where(t => ModuleDatabaseBackupTarget.IsValidKey(t.Key)).DistinctBy(t => t.Key).ToList();

    public IReadOnlyList<ModuleDatabaseBackupTarget> Targets => _targets;

    public async Task<DatabaseBackupResult> BackupAsync(bool differential, string? target = null, CancellationToken cancellationToken = default)
    {
        var (connectionString, database) = ResolveTarget(target);

        var gate = BackupGates.GetOrAdd(database, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false))
        {
            throw new DatabaseBackupException($"資料庫 {database} 已有備份作業執行中，請稍後再試。");
        }

        try
        {
            return await BackupCoreAsync(connectionString, database, differential, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// 主資料庫：BackupConnection（未設定時回退）。模組目標：同一登入帳號，資料庫改為目標連線字串的資料庫，
    /// 避免為每個模組維護另一組備份機密（登入須為該資料庫的 db_backupoperator）。
    /// </summary>
    private (string ConnectionString, string Database) ResolveTarget(string? targetKey)
    {
        var baseConnection = ResolveConnectionString();
        var mainDatabase = new SqlConnectionStringBuilder(baseConnection).InitialCatalog;
        if (string.IsNullOrWhiteSpace(targetKey))
        {
            return string.IsNullOrWhiteSpace(mainDatabase)
                ? throw new DatabaseBackupException("備份連線字串未指定資料庫 (Database / Initial Catalog)。")
                : (baseConnection, mainDatabase);
        }

        var target = _targets.FirstOrDefault(t => string.Equals(t.Key, targetKey.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new DatabaseBackupException($"找不到備份目標 {targetKey}（模組未安裝或未啟用）。");

        string? database;
        try
        {
            database = new SqlConnectionStringBuilder(configuration[target.ConnectionStringKey] ?? string.Empty).InitialCatalog;
        }
        catch (ArgumentException ex)
        {
            throw new DatabaseBackupException($"{target.DisplayName}的連線字串格式錯誤（{target.ConnectionStringKey}）。", ex);
        }

        if (string.IsNullOrWhiteSpace(database))
        {
            throw new DatabaseBackupException($"{target.DisplayName}未設定資料庫（{target.ConnectionStringKey}）。");
        }

        if (string.Equals(database, mainDatabase, StringComparison.OrdinalIgnoreCase))
        {
            throw new DatabaseBackupException($"{target.DisplayName}與主資料庫相同（{database}），已由主資料庫備份涵蓋。");
        }

        return (new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = database }.ConnectionString, database);
    }

    private async Task<DatabaseBackupResult> BackupCoreAsync(string connectionString, string database, bool differential, CancellationToken cancellationToken)
    {
        var options = schedulerOptions.CurrentValue.DatabaseBackup;

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
                4060 => $"備份帳號無法存取資料庫 {database}，請於該資料庫建立使用者並授予 db_backupoperator（docs/sql/20261105_grant_module_database_backup.sql）。",
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
