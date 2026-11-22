using Hangfire;
using Microsoft.Extensions.Logging;
using SGS.Modules.SCH.Application.Abstractions;
using SGS.Modules.SCH.Application.Http;

namespace SGS.Modules.SCH.Application.Jobs;

/// <summary>
/// MTG 過期錄音清除（mtg-audio-retention）：以系統帳號 (scope: mtg.audio.purge) 呼叫 POST api/v1/mtg/audio/purge-expired，
/// 把超過 Meeting:AudioRetentionDays 天的會議錄音從檔案管理刪除（逐字稿與紀要保留）。預設每日 04:30；
/// 端點由 SGS.Modules.MTG 外掛提供，僅在 Meeting:AudioRetentionDays 大於 0 時註冊。
/// </summary>
public sealed class MtgAudioRetentionJob(SchedulerApiClient apiClient, ILogger<MtgAudioRetentionJob> logger) : IRecurringJob
{
    public const string JobId = "mtg-audio-retention";

    private sealed record PurgeResponse(int RetentionDays, int Candidates, int Expired, int Failed, string Message);

    [JobDisplayName("MTG 過期錄音清除")]
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [300, 900], OnlyOn = [typeof(TransientScheduledJobException), typeof(HttpRequestException), typeof(TaskCanceledException)])]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task<string?> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await apiClient.PostAsync<PurgeResponse>("api/v1/mtg/audio/purge-expired", null, TimeSpan.FromMinutes(10), cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[Scheduler] MTG 過期錄音清除：{Message}", result.Message);
        return result.Message;
    }
}
