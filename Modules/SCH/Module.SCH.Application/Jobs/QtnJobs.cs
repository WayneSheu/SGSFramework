using Hangfire;
using Microsoft.Extensions.Logging;
using SGS.Modules.SCH.Application.Abstractions;
using SGS.Modules.SCH.Application.Http;

namespace SGS.Modules.SCH.Application.Jobs;

/// <summary>QTN 掃描端點的共同回應（ScanResultDto）。</summary>
internal sealed record QtnScanResponse(int Processed, int Notified, string Message);

/// <summary>
/// 報價有效期到期掃描（qtn-quote-expiry）：以系統帳號 (scope: qtn.quote.scan) 呼叫 POST api/v1/qtn/quotes/scan-expiry，
/// 已發出且逾有效期的報價轉為逾期，並提醒 7 日內到期者。預設每日 07:30；端點由 SGS.Modules.QTN 外掛提供，需 Quotation:Enabled。
/// </summary>
public sealed class QtnQuoteExpiryJob(SchedulerApiClient apiClient, ILogger<QtnQuoteExpiryJob> logger) : IRecurringJob
{
    public const string JobId = "qtn-quote-expiry";

    [JobDisplayName("QTN 報價到期掃描")]
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [300, 900], OnlyOn = [typeof(TransientScheduledJobException), typeof(HttpRequestException), typeof(TaskCanceledException)])]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task<string?> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await apiClient.PostAsync<QtnScanResponse>("api/v1/qtn/quotes/scan-expiry", null, TimeSpan.FromMinutes(2), cancellationToken).ConfigureAwait(false);
        logger.LogInformation("[Scheduler] QTN 報價到期掃描：{Message}", result.Message);
        return result.Message;
    }
}

/// <summary>
/// CR 變更評估逾期提醒（qtn-cr-overdue）：以系統帳號 (scope: qtn.cr.scan) 呼叫 POST api/v1/qtn/change-assessments/scan-overdue，
/// 提醒收到請求後逾 3 個工作日仍未完成《變更影響評估單》者。預設平日 09:00；需 Quotation:Enabled。
/// </summary>
public sealed class QtnCrOverdueJob(SchedulerApiClient apiClient, ILogger<QtnCrOverdueJob> logger) : IRecurringJob
{
    public const string JobId = "qtn-cr-overdue";

    [JobDisplayName("QTN CR 評估逾期提醒")]
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task<string?> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await apiClient.PostAsync<QtnScanResponse>("api/v1/qtn/change-assessments/scan-overdue", null, TimeSpan.FromMinutes(2), cancellationToken).ConfigureAwait(false);
        logger.LogInformation("[Scheduler] QTN CR 評估逾期提醒：{Message}", result.Message);
        return result.Message;
    }
}

/// <summary>
/// 結案復盤逾期提醒（qtn-variance-due）：以系統帳號 (scope: qtn.review.scan) 呼叫 POST api/v1/qtn/variance-reviews/scan-due，
/// 提醒逾 14 個工作日未產出對比報告、或逾 7 日未校準標準工時庫者。預設平日 09:30；需 Quotation:Enabled。
/// </summary>
public sealed class QtnVarianceDueJob(SchedulerApiClient apiClient, ILogger<QtnVarianceDueJob> logger) : IRecurringJob
{
    public const string JobId = "qtn-variance-due";

    [JobDisplayName("QTN 結案復盤逾期提醒")]
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task<string?> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await apiClient.PostAsync<QtnScanResponse>("api/v1/qtn/variance-reviews/scan-due", null, TimeSpan.FromMinutes(2), cancellationToken).ConfigureAwait(false);
        logger.LogInformation("[Scheduler] QTN 結案復盤逾期提醒：{Message}", result.Message);
        return result.Message;
    }
}
