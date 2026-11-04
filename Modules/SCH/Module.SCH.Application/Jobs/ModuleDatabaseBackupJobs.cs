using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGS.Modules.SCH.Application.Abstractions;
using SGS.Modules.SCH.Application.Http;
using SGS.Modules.SCH.Application.Maintenance;
using SGS.Modules.SCH.Application.Options;
using SGSFramework.Core.Scheduling;

namespace SGS.Modules.SCH.Application.Jobs;

/// <summary>
/// 外掛模組自有資料庫的備份作業：每個 <see cref="ModuleDatabaseBackupTarget"/> 產生完整 / 差異兩個週期作業，
/// 以系統帳號 (scope: database.backup) 呼叫 POST api/v1/database-maintenance/backups（target = 模組 Key）。
/// </summary>
public static class ModuleDatabaseBackupJobs
{
    public const string FullJobIdPrefix = "database-backup-full-";
    public const string DifferentialJobIdPrefix = "database-backup-differential-";

    /// <summary>依備份目標產生作業描述；Key 格式不符或重複的目標略過（不影響其他作業）</summary>
    public static IEnumerable<RecurringJobDescriptor> Describe(IEnumerable<ModuleDatabaseBackupTarget> targets)
    {
        foreach (var target in targets.Where(t => ModuleDatabaseBackupTarget.IsValidKey(t.Key)).DistinctBy(t => t.Key))
        {
            yield return new RecurringJobDescriptor(
                FullJobIdPrefix + target.Key, typeof(ModuleDatabaseFullBackupJob),
                $"{target.DisplayName}完整備份", target.DefaultFullCron, EnabledByDefault: true,
                Factory: sp => ActivatorUtilities.CreateInstance<ModuleDatabaseFullBackupJob>(sp, target));

            yield return new RecurringJobDescriptor(
                DifferentialJobIdPrefix + target.Key, typeof(ModuleDatabaseDifferentialBackupJob),
                $"{target.DisplayName}差異備份", target.DefaultDifferentialCron, EnabledByDefault: false,
                Factory: sp => ActivatorUtilities.CreateInstance<ModuleDatabaseDifferentialBackupJob>(sp, target));
        }
    }
}

/// <summary>模組資料庫備份作業基底</summary>
public abstract class ModuleDatabaseBackupJobBase(
    ModuleDatabaseBackupTarget target,
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
            "api/v1/database-maintenance/backups", new { Differential, Target = target.Key }, timeout, cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[Scheduler] {Target}備份完成：{Database} {Type} → {Path}（驗證：{Verified}，大小：{Size}）",
            target.DisplayName, result.Database, result.BackupType, result.FilePath, result.Verified, result.SizeBytes);

        return $"{result.FilePath}（{(result.Verified ? "已驗證" : "未驗證")}"
               + (result.SizeBytes is { } size ? $"，{size / 1024.0 / 1024.0:0.#} MB" : null)
               + (result.ExpiredFilesDeleted > 0 ? $"，清除過期備份 {result.ExpiredFilesDeleted} 個" : null) + "）";
    }
}

/// <summary>模組資料庫完整備份（重試策略與主庫完整備份相同）</summary>
public sealed class ModuleDatabaseFullBackupJob(
    ModuleDatabaseBackupTarget target, SchedulerApiClient apiClient, IOptionsMonitor<SchedulerOptions> options, ILogger<ModuleDatabaseFullBackupJob> logger)
    : ModuleDatabaseBackupJobBase(target, apiClient, options, logger)
{
    protected override bool Differential => false;

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [600, 1800], OnlyOn = [typeof(TransientScheduledJobException), typeof(HttpRequestException), typeof(TaskCanceledException)])]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public override Task<string?> ExecuteAsync(CancellationToken cancellationToken) => RunAsync(cancellationToken);
}

/// <summary>模組資料庫差異備份（需搭配非 COPY_ONLY 的完整備份）</summary>
public sealed class ModuleDatabaseDifferentialBackupJob(
    ModuleDatabaseBackupTarget target, SchedulerApiClient apiClient, IOptionsMonitor<SchedulerOptions> options, ILogger<ModuleDatabaseDifferentialBackupJob> logger)
    : ModuleDatabaseBackupJobBase(target, apiClient, options, logger)
{
    protected override bool Differential => true;

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [300, 900], OnlyOn = [typeof(TransientScheduledJobException), typeof(HttpRequestException), typeof(TaskCanceledException)])]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public override Task<string?> ExecuteAsync(CancellationToken cancellationToken) => RunAsync(cancellationToken);
}
