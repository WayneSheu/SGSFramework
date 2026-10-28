using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.Scheduler.Abstractions;
using SGSFramework.Scheduler.Hosting;
using SGSFramework.Scheduler.Http;
using SGSFramework.Scheduler.Options;

namespace SGSFramework.Scheduler.Jobs;

/// <summary>
/// 定期權限覆核：以系統帳號 (scope: accessreview.campaign.schedule) 呼叫
/// POST api/v1/access-reviews/campaigns/scheduled 建立本期覆核活動。
/// 活動名稱含期間（例如「定期權限覆核 2026-Q4」），同期間重試不會重複建立。
/// </summary>
public sealed class AccessReviewCampaignJob(
    SchedulerApiClient apiClient,
    IOptionsMonitor<SchedulerOptions> options,
    TimeProvider timeProvider,
    ILogger<AccessReviewCampaignJob> logger) : IRecurringJob
{
    public const string JobId = "access-review-campaign";

    private sealed record ScheduledResponse(bool Succeeded, string Message, Guid? CampaignId, bool Created, int ClosedOverdue);

    [JobDisplayName("定期權限覆核")]
    [AutomaticRetry(Attempts = 3, OnlyOn = [typeof(TransientScheduledJobException), typeof(HttpRequestException), typeof(TaskCanceledException)])]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task<string?> ExecuteAsync(CancellationToken cancellationToken)
    {
        var current = options.CurrentValue;
        var settings = current.AccessReview;
        var timeZone = SchedulerTimeZones.Resolve(current.Jobs.GetValueOrDefault(JobId)?.TimeZone ?? current.TimeZone);
        var localNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone);
        var name = $"{settings.NamePrefix.Trim()} {FormatPeriod(localNow, settings.Period)}";

        var result = await apiClient.PostAsync<ScheduledResponse>(
            "api/v1/access-reviews/campaigns/scheduled",
            new
            {
                Name = name,
                settings.Description,
                settings.DueInDays,
                RoleIds = settings.RoleIds,
                CloseOverdue = settings.CloseOverdueCampaigns
            },
            TimeSpan.FromMinutes(10),
            cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[Scheduler] 定期權限覆核「{CampaignName}」：{Message}（新建：{Created}，結束逾期活動：{Closed}）",
            name, result.Message, result.Created, result.ClosedOverdue);

        return result.ClosedOverdue > 0 ? $"{result.Message}（結束逾期活動 {result.ClosedOverdue} 個）" : result.Message;
    }

    internal static string FormatPeriod(DateTimeOffset localNow, AccessReviewPeriod period) => period switch
    {
        AccessReviewPeriod.Month => $"{localNow:yyyy-MM}",
        AccessReviewPeriod.Quarter => $"{localNow.Year}-Q{(localNow.Month - 1) / 3 + 1}",
        AccessReviewPeriod.HalfYear => $"{localNow.Year}-H{(localNow.Month - 1) / 6 + 1}",
        AccessReviewPeriod.Year => $"{localNow.Year}",
        _ => $"{localNow:yyyy-MM-dd}"
    };
}
