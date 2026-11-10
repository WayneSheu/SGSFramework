using Hangfire;
using Microsoft.Extensions.Logging;
using SGS.Modules.SCH.Application.Abstractions;
using SGS.Modules.SCH.Application.Http;

namespace SGS.Modules.SCH.Application.Jobs;

/// <summary>
/// 日誌庫年度換庫「準備階段」：以系統帳號 (scope: logs.rotation.prepare) 呼叫 POST api/v1/log-rotation/prepare，
/// 完整備份熱庫並還原為「還原中」的冷庫，等待管理者在「日誌換庫管理」核准後才凍結、換庫、刪除舊熱庫（不可逆步驟不自動執行）。
/// 預設每年 1/1 01:00（年初封存上一年度；LogRotation:Granularity = HalfYear 時改為每年 1/1 與 7/1 各一次，需自訂排程週期）；僅於設定換庫帳號連線 (RotationConnection) 時註冊。
/// 呼叫端點的失敗（未設定、權限、已有待核准項目）為 4xx，不重試，由排程失敗告警通知管理者。
/// </summary>
public sealed class LogRotationPrepareJob(SchedulerApiClient apiClient, ILogger<LogRotationPrepareJob> logger) : IRecurringJob
{
    public const string JobId = "log-rotation-prepare";

    /// <summary>端點回應（只取摘要需要的欄位；System.Text.Json 預設不分大小寫）</summary>
    private sealed record PrepareResponse(string StagedDatabase, string Period, string PeriodDisplay, string BackupFile);

    [JobDisplayName("日誌庫換庫準備")]
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task<string?> ExecuteAsync(CancellationToken cancellationToken)
    {
        // 逾時：完整備份 + 驗證 + 還原，日誌庫較小，但預留數小時
        var result = await apiClient.PostAsync<PrepareResponse>(
            "api/v1/log-rotation/prepare", new { Period = (string?)null }, TimeSpan.FromHours(4), cancellationToken).ConfigureAwait(false);

        logger.LogWarning("[Scheduler] 日誌庫換庫準備完成：冷庫 {Cold}（{Period}）待管理者核准，備份檔 {File}", result.StagedDatabase, result.PeriodDisplay, result.BackupFile);
        return $"冷庫 {result.StagedDatabase}（{result.PeriodDisplay}）已備妥，請至「日誌換庫管理」核准執行；備份檔 {result.BackupFile}";
    }
}
