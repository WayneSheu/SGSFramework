using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGSFramework.Scheduler.Services;

namespace SGSFramework.Scheduler.Hosting;

internal static class SchedulerTimeZones
{
    /// <summary>解析 IANA / Windows 時區 ID；找不到時回退主機時區（避免設定錯誤導致排程無法啟動）</summary>
    public static TimeZoneInfo Resolve(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Local;
        return TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) ? zone : TimeZoneInfo.Local;
    }
}

/// <summary>
/// 啟動時將程式註冊的週期作業同步至 Hangfire：套用 Scheduler:Jobs 的 Cron / 時區 / 啟用覆寫與排程管理頁的自訂週期、暫停狀態。
/// Hangfire 以分散式鎖確保多節點只排入一次。過期執行紀錄由「日誌清除」作業處理。
/// </summary>
internal sealed class RecurringJobRegistrar(
    IServiceScopeFactory scopeFactory,
    ILogger<RecurringJobRegistrar> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<IScheduledJobManager>().SyncAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 資料庫暫時無法連線時不阻斷 API 啟動；週期作業維持上次同步的狀態
            logger.LogError(ex, "[Scheduler] 同步週期作業失敗。");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
