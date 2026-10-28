using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.Scheduler.Abstractions;
using SGSFramework.Scheduler.Http;
using SGSFramework.Scheduler.Maintenance;
using SGSFramework.Scheduler.Options;

namespace SGSFramework.Scheduler.Jobs;

/// <summary>
/// 資料庫備份作業基底：以系統帳號 (scope: database.backup) 呼叫 POST api/v1/database-maintenance/backups。
/// </summary>
public abstract class DatabaseBackupJobBase(
    SchedulerApiClient apiClient,
    IOptionsMonitor<SchedulerOptions> options,
    ILogger logger) : IRecurringJob
{
    protected abstract bool Differential { get; }

    public abstract Task<string?> ExecuteAsync(CancellationToken cancellationToken);

    protected async Task<string?> RunAsync(CancellationToken cancellationToken)
    {
        // 逾時 = SQL 指令逾時 (BACKUP + VERIFY) 再加緩衝，避免 HTTP 先於 SQL 中斷
        var commandTimeout = TimeSpan.FromSeconds(options.CurrentValue.DatabaseBackup.CommandTimeoutSeconds);
        var timeout = commandTimeout * 2 + TimeSpan.FromMinutes(1);

        var result = await apiClient.PostAsync<DatabaseBackupResult>(
            "api/v1/database-maintenance/backups", new { Differential }, timeout, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[Scheduler] 資料庫備份完成：{Database} {Type} → {Path}（驗證：{Verified}，大小：{Size}）",
            result.Database, result.BackupType, result.FilePath, result.Verified, result.SizeBytes);

        return $"{result.FilePath}（{(result.Verified ? "已驗證" : "未驗證")}"
               + (result.SizeBytes is { } size ? $"，{size / 1024.0 / 1024.0:0.#} MB" : null)
               + (result.ExpiredFilesDeleted > 0 ? $"，清除過期備份 {result.ExpiredFilesDeleted} 個" : null) + "）";
    }
}

/// <summary>資料庫完整備份</summary>
public sealed class DatabaseFullBackupJob(
    SchedulerApiClient apiClient, IOptionsMonitor<SchedulerOptions> options, ILogger<DatabaseFullBackupJob> logger)
    : DatabaseBackupJobBase(apiClient, options, logger)
{
    public const string JobId = "database-backup-full";

    protected override bool Differential => false;

    [JobDisplayName("資料庫完整備份")]
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [600, 1800], OnlyOn = [typeof(TransientScheduledJobException), typeof(HttpRequestException), typeof(TaskCanceledException)])]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public override Task<string?> ExecuteAsync(CancellationToken cancellationToken) => RunAsync(cancellationToken);
}

/// <summary>資料庫差異備份（需搭配完整備份，且完整備份不可為 COPY_ONLY）</summary>
public sealed class DatabaseDifferentialBackupJob(
    SchedulerApiClient apiClient, IOptionsMonitor<SchedulerOptions> options, ILogger<DatabaseDifferentialBackupJob> logger)
    : DatabaseBackupJobBase(apiClient, options, logger)
{
    public const string JobId = "database-backup-differential";

    protected override bool Differential => true;

    [JobDisplayName("資料庫差異備份")]
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [300, 900], OnlyOn = [typeof(TransientScheduledJobException), typeof(HttpRequestException), typeof(TaskCanceledException)])]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public override Task<string?> ExecuteAsync(CancellationToken cancellationToken) => RunAsync(cancellationToken);
}
