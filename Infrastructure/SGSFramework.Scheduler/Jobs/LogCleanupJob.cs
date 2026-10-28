using Hangfire;
using Microsoft.Extensions.Logging;
using SGSFramework.Scheduler.Abstractions;
using SGSFramework.Scheduler.Http;
using SGSFramework.Scheduler.Maintenance;

namespace SGSFramework.Scheduler.Jobs;

/// <summary>
/// 日誌清除：以系統帳號 (scope: logs.cleanup) 呼叫 POST api/v1/database-maintenance/log-cleanup，
/// 依 Scheduler:LogCleanup 保留天數分批刪除過期紀錄。預設每天 03:30（避開 02:00 完整備份）。
/// </summary>
public sealed class LogCleanupJob(SchedulerApiClient apiClient, ILogger<LogCleanupJob> logger) : IRecurringJob
{
    public const string JobId = "log-cleanup";

    [JobDisplayName("日誌清除")]
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [600, 1800], OnlyOn = [typeof(TransientScheduledJobException), typeof(HttpRequestException), typeof(TaskCanceledException)])]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task<string?> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await apiClient.PostAsync<LogCleanupResult>(
            "api/v1/database-maintenance/log-cleanup", null, TimeSpan.FromMinutes(30), cancellationToken).ConfigureAwait(false);

        var details = result.Targets.Where(t => t.Deleted > 0).Select(t => $"{t.Description} {t.Deleted:N0}").ToList();
        var failures = result.Targets.Where(t => t.Note?.StartsWith("清除失敗", StringComparison.Ordinal) == true)
            .Select(t => t.Description).ToList();

        logger.LogInformation("[Scheduler] 日誌清除完成：共刪除 {Total} 筆。", result.TotalDeleted);

        return $"共刪除 {result.TotalDeleted:N0} 筆"
               + (details.Count > 0 ? $"（{string.Join("、", details)}）" : null)
               + (failures.Count > 0 ? $"；部分失敗：{string.Join("、", failures)}" : null);
    }
}
