using Hangfire;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.FileManagement;
using SGS.Modules.SCH.Application.Abstractions;
using SGS.Modules.SCH.Application.Http;

namespace SGS.Modules.SCH.Application.Jobs;

/// <summary>
/// 已刪除檔案清除：以系統帳號 (scope: files.purge) 呼叫 POST api/v1/files/purge，
/// 刪除保留期滿（FileManagement:Purge:RetentionDays）的虛擬刪除檔案實體。預設每天 04:00（避開備份與日誌清除）。
/// 端點由檔案管理外掛模組 SGS.Modules.FMS 提供（跨模組僅經 HTTP 與 Core 契約 FilePurgeResult）。
/// </summary>
public sealed class FilePurgeJob(SchedulerApiClient apiClient, ILogger<FilePurgeJob> logger) : IRecurringJob
{
    public const string JobId = "file-purge";

    [JobDisplayName("已刪除檔案清除")]
    [AutomaticRetry(Attempts = 2, DelaysInSeconds = [600, 1800], OnlyOn = [typeof(TransientScheduledJobException), typeof(HttpRequestException), typeof(TaskCanceledException)])]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task<string?> ExecuteAsync(CancellationToken cancellationToken)
    {
        var result = await apiClient.PostAsync<FilePurgeResult>(
            "api/v1/files/purge", null, TimeSpan.FromMinutes(30), cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[Scheduler] 已刪除檔案清除完成：清除 {Purged} 筆、失敗 {Failed} 筆。", result.Purged, result.Failed);

        return $"清除 {result.Purged:N0} 筆（保留 {result.RetentionDays} 天）"
               + (result.ThumbnailsRemoved > 0 ? $"，移除縮圖 {result.ThumbnailsRemoved:N0} 個" : null)
               + (result.Failed > 0 ? $"；失敗 {result.Failed:N0} 筆，下次重試" : null);
    }
}
