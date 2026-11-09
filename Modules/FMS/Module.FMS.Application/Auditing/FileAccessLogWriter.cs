using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGS.Modules.FMS.Infrastructure.Dbcontexts;
using SGS.Modules.FMS.Infrastructure.Entities;

namespace SGS.Modules.FMS.Application.Auditing;

/// <summary>
/// 稽核紀錄批次寫入器（BackgroundService）：自佇列取出紀錄，每批最多 <see cref="BatchSize"/> 筆寫入 Ledger 資料表。
/// 寫入失敗以指數退讓重試；關機時排空佇列後才結束，避免遺失稽核軌跡。
/// </summary>
public sealed class FileAccessLogWriter(
    FileAccessLogQueue queue,
    IDbContextFactory<FMSDbContext> dbContextFactory,
    ILogger<FileAccessLogWriter> logger) : BackgroundService
{
    public const int BatchSize = 200;
    private const int MaxAttempts = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<FileAccessLog>(BatchSize);
        try
        {
            while (await queue.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
            {
                await DrainAsync(batch, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 關機：排空剩餘紀錄
            await DrainAsync(batch, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        queue.Writer.TryComplete();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>寫入佇列中目前所有紀錄（測試與關機排空共用）</summary>
    internal async Task DrainAsync(List<FileAccessLog> batch, CancellationToken cancellationToken)
    {
        while (true)
        {
            batch.Clear();
            while (batch.Count < BatchSize && queue.Reader.TryRead(out var entry)) batch.Add(entry);
            if (batch.Count == 0) return;

            await WriteWithRetryAsync(batch, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WriteWithRetryAsync(List<FileAccessLog> batch, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
                db.FileAccessLogs.AddRange(batch);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && attempt < MaxAttempts)
            {
                logger.LogWarning(ex, "[FileAccessLog] 寫入稽核紀錄失敗（第 {Attempt} 次），{Count} 筆稍後重試。", attempt, batch.Count);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 最終失敗：完整內容寫入系統日誌，保留稽核證據供補登
                foreach (var e in batch)
                {
                    logger.LogCritical(ex, "[FileAccessLog] 稽核紀錄寫入失敗：{Action} {IsSuccess} 檔案 {FileId} 分類 {Category} 使用者 {UserId}/{UserName} 系統帳號 {IsSystemAccount} {ClientId} IP {RemoteIp} 追蹤碼 {TraceId} {Detail} {OccurredAt:o}",
                        e.Action, e.IsSuccess, e.FileId, e.Category, e.UserId, e.UserName, e.IsSystemAccount, e.ClientId, e.RemoteIp, e.TraceId, e.Detail, e.OccurredAt);
                }

                return;
            }
        }
    }
}
