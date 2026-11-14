using Hangfire;
using Microsoft.Extensions.Logging;
using SGS.Modules.SCH.Application.Abstractions;
using SGS.Modules.SCH.Application.Http;

namespace SGS.Modules.SCH.Application.Jobs;

/// <summary>
/// PMS CR 評估逾期提醒（pms-cr-eval-watch）：以系統帳號 (scope: pms.cr.scan) 呼叫
/// POST api/v1/pms/change-requests/scan-overdue，由專案管理模組找出受理後超過 2 個工作天仍未評估的 CR，
/// 依專案寄信給 Tech Lead 與 PM（同一張 CR 每個台灣日最多一封；非工作日不發）。預設平日 09:00。
/// 端點由 SGS.Modules.PMS 外掛提供（跨模組僅經 HTTP）。
/// </summary>
public sealed class PmsCrEvalWatchJob(SchedulerApiClient apiClient, ILogger<PmsCrEvalWatchJob> logger) : IRecurringJob
{
    public const string JobId = "pms-cr-eval-watch";

    private sealed record WatchResponse(int Overdue, int Notified, int ProjectsNotified, int Skipped, string Message);

    [JobDisplayName("PMS CR 評估逾期提醒")]
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [300, 900], OnlyOn = [typeof(TransientScheduledJobException), typeof(HttpRequestException), typeof(TaskCanceledException)])]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task<string?> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await apiClient.PostAsync<WatchResponse>(
            "api/v1/pms/change-requests/scan-overdue", null, TimeSpan.FromMinutes(5), cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[Scheduler] PMS CR 評估逾期提醒完成：{Message}", result.Message);
        return result.Message;
    }
}
