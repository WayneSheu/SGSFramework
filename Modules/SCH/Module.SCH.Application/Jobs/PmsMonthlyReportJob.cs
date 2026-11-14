using Hangfire;
using Microsoft.Extensions.Logging;
using SGS.Modules.SCH.Application.Abstractions;
using SGS.Modules.SCH.Application.Http;

namespace SGS.Modules.SCH.Application.Jobs;

/// <summary>
/// PMS 月度維運報告自動產生（pms-report-monthly）：以系統帳號 (scope: pms.report.monthly) 呼叫
/// POST api/v1/pms/reports/monthly/auto-generate。作業每天執行，由專案管理模組依工作日曆判斷今天是否為產生日：
/// 月底最後一個工作日產生當月月報、月初前 3 個工作日補產上月缺漏者；只處理進行中且在維運期的專案；
/// 已有月報者不覆蓋；產生後通知該專案 PM／Tech Lead。預設每天 18:00（台灣時間）。
/// 端點由 SGS.Modules.PMS 外掛提供（跨模組僅經 HTTP）。
/// </summary>
public sealed class PmsMonthlyReportJob(SchedulerApiClient apiClient, ILogger<PmsMonthlyReportJob> logger) : IRecurringJob
{
    public const string JobId = "pms-report-monthly";

    private sealed record AutoGenerateResponse(
        List<string> Periods, int Eligible, int Generated, int SkippedExisting, int Failed, int Notified, string Message);

    [JobDisplayName("PMS 月度維運報告自動產生")]
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [600, 1800], OnlyOn = [typeof(TransientScheduledJobException), typeof(HttpRequestException), typeof(TaskCanceledException)])]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task<string?> ExecuteAsync(CancellationToken cancellationToken)
    {
        // 逐專案產生 PDF 並寫入 FMS，專案多時需要較長逾時
        var result = await apiClient.PostAsync<AutoGenerateResponse>(
            "api/v1/pms/reports/monthly/auto-generate", null, TimeSpan.FromMinutes(20), cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[Scheduler] PMS 月報自動產生完成：{Message}", result.Message);
        return result.Message;
    }
}
