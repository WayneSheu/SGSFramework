using Hangfire;
using Microsoft.Extensions.Logging;
using SGS.Modules.SCH.Application.Abstractions;
using SGS.Modules.SCH.Application.Http;

namespace SGS.Modules.SCH.Application.Jobs;

/// <summary>
/// PMS 合約履約每日監看（pms-contract-watch）：以系統帳號 (scope: pms.contract.watch) 呼叫 POST api/v1/pms/contracts/watch，
/// 由專案管理模組處理甲方協力遲延提醒、文件審查逾期擬制通過、擬制驗收、付款期別放行與請款提醒（同一事項每個台灣日最多提醒一次；非工作日不執行）。
/// 預設平日 09:00。端點由 SGS.Modules.PMS 外掛提供（跨模組僅經 HTTP）。
/// </summary>
public sealed class PmsContractWatchJob(SchedulerApiClient apiClient, ILogger<PmsContractWatchJob> logger) : IRecurringJob
{
    public const string JobId = "pms-contract-watch";

    private sealed record WatchResponse(int CoopReminded, int ReviewsDeemed, int AcceptancesDeemed, int PaymentsPromoted, int PaymentsReminded, int Skipped, string Message);

    [JobDisplayName("PMS 合約履約每日監看")]
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [300, 900], OnlyOn = [typeof(TransientScheduledJobException), typeof(HttpRequestException), typeof(TaskCanceledException)])]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task<string?> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await apiClient.PostAsync<WatchResponse>(
            "api/v1/pms/contracts/watch", null, TimeSpan.FromMinutes(5), cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[Scheduler] PMS 合約履約監看完成：{Message}", result.Message);
        return result.Message;
    }
}
