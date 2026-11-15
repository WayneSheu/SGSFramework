using Hangfire;
using Microsoft.Extensions.Logging;
using SGS.Modules.SCH.Application.Abstractions;
using SGS.Modules.SCH.Application.Http;

namespace SGS.Modules.SCH.Application.Jobs;

/// <summary>
/// 政府辦公日曆每月自動同步（cal-holiday-sync）：以系統帳號 (scope: cal.holiday.sync) 呼叫 POST api/v1/cal/holidays/sync-gov/scan，
/// 由行事曆管理模組同步當年與明年的行政機關辦公日曆（只新增與更新「同步來源」的資料，不覆寫人工維護；明年尚未公布不算失敗）。
/// 預設每月 1 日 03:00。端點由 SGS.Modules.CAL 外掛提供（跨模組僅經 HTTP）；需 Calendar:Enabled 且伺服器可連外網。
/// </summary>
public sealed class CalHolidaySyncJob(SchedulerApiClient apiClient, ILogger<CalHolidaySyncJob> logger) : IRecurringJob
{
    public const string JobId = "cal-holiday-sync";

    private sealed record ScanResponse(int Years, int Added, int Updated, int Removed, int SkippedManual, int Failed, string Message);

    [JobDisplayName("CAL 政府辦公日曆同步")]
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [600, 1800], OnlyOn = [typeof(TransientScheduledJobException), typeof(HttpRequestException), typeof(TaskCanceledException)])]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task<string?> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await apiClient.PostAsync<ScanResponse>(
            "api/v1/cal/holidays/sync-gov/scan", null, TimeSpan.FromMinutes(5), cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[Scheduler] CAL 政府辦公日曆同步完成：{Message}", result.Message);

        // 來源連線或資料有問題時讓作業失敗，才會出現在執行紀錄與失敗告警（明年尚未公布不算失敗）
        if (result.Failed > 0) throw new InvalidOperationException($"政府辦公日曆同步有 {result.Failed} 個年度失敗：{result.Message}");

        return result.Message;
    }
}

/// <summary>
/// 行事曆活動提醒派送（cal-event-reminders）：以系統帳號 (scope: cal.reminders.dispatch) 每 5 分鐘呼叫
/// POST api/v1/cal/calendar/reminders/dispatch，寄 Email 提醒活動建立者與與會者（每組提醒只寄一次，活動開始後不再提醒）。
/// 端點由 SGS.Modules.CAL 外掛提供（跨模組僅經 HTTP）；需 Calendar:Enabled。
/// </summary>
public sealed class CalEventReminderJob(SchedulerApiClient apiClient, ILogger<CalEventReminderJob> logger) : IRecurringJob
{
    public const string JobId = "cal-event-reminders";

    private sealed record DispatchResponse(int Sent, int NoRecipients, int Failed, string Message);

    [JobDisplayName("CAL 活動提醒派送")]
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task<string?> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await apiClient.PostAsync<DispatchResponse>(
            "api/v1/cal/calendar/reminders/dispatch", null, TimeSpan.FromMinutes(2), cancellationToken).ConfigureAwait(false);

        // 有寄出或失敗才寫 Information；全部為 0 時降為 Debug，避免每 5 分鐘洗版
        if (result.Sent > 0 || result.Failed > 0) logger.LogInformation("[Scheduler] CAL 活動提醒：{Message}", result.Message);
        else logger.LogDebug("[Scheduler] CAL 活動提醒：{Message}", result.Message);

        return result.Message;
    }
}
