using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SGS.Modules.MTG.Application.Rules;
using SGS.Modules.MTG.Infrastructure.Dbcontexts;
using SGS.Modules.MTG.Infrastructure.Entities;

namespace SGS.Modules.MTG.Application.Services;

/// <summary>
/// 背景工作者：依序（一次一個，避免同時載入多份模型 context 耗盡共享記憶體）處理紀要佇列。
/// 流程：讀逐字稿 → Map-Reduce 摘要 → 新增紀要版本 → 更新會議狀態。失敗只記錄安全訊息，不含逐字稿或模型內容。
/// </summary>
public sealed class MinutesWorker(
    MinutesQueue queue,
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<MinutesWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverInterruptedAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            MinutesWorkItem item;
            try
            {
                item = await queue.DequeueAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await ProcessAsync(item, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // 單一作業失敗不得讓整個工作者結束
                logger.LogError(ex, "[MTG] 紀要作業（會議 {MeetingId}）發生未預期例外。", item.MeetingId);
            }
        }
    }

    /// <summary>服務重啟時，佇列內容已遺失；仍停在「產生中」的會議標記為失敗，讓使用者重新產生（逐字稿在資料庫，不會遺失）。</summary>
    private async Task RecoverInterruptedAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MTGDbContext>();
            var stale = await db.Meetings.Where(m => m.Status == MeetingStatus.Summarizing).ToListAsync(ct).ConfigureAwait(false);
            foreach (var meeting in stale)
            {
                meeting.Status = MeetingStatus.Failed;
                meeting.StatusMessage = "服務重新啟動，紀要產生中斷，請重新產生。";
                meeting.UpdatedAtUtc = clock.GetUtcNow();
            }

            if (stale.Count > 0)
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                logger.LogWarning("[MTG] 已將 {Count} 筆中斷的紀要作業標記為失敗。", stale.Count);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 資料庫尚未就緒等情況不應讓整個主機啟動失敗
            logger.LogWarning(ex, "[MTG] 啟動時回復中斷作業失敗。");
        }
    }

    private async Task ProcessAsync(MinutesWorkItem item, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MTGDbContext>();
        var summarizer = scope.ServiceProvider.GetRequiredService<MapReduceSummarizer>();
        var generator = scope.ServiceProvider.GetRequiredService<ITextGenerator>();

        var meeting = await db.Meetings.FirstOrDefaultAsync(m => m.Id == item.MeetingId, ct).ConfigureAwait(false);
        if (meeting is null) return; // 排隊期間會議已被刪除

        try
        {
            var template = await db.PromptTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.ScenarioCode == item.ScenarioCode, ct).ConfigureAwait(false)
                           ?? throw new InvalidOperationException($"找不到場景「{item.ScenarioCode}」的模板。");

            var segments = await db.TranscriptSegments.AsNoTracking().Where(s => s.MeetingId == item.MeetingId).OrderBy(s => s.Seq)
                .Select(s => new { s.Speaker, Text = s.CorrectedText ?? s.Text })
                .ToListAsync(ct).ConfigureAwait(false);
            var transcript = TranscriptChunker.BuildTranscriptText(segments.Select(s => (s.Speaker, s.Text)));

            var markdown = await summarizer.SummarizeAsync(transcript, new SummaryTemplate(template.MapPrompt, template.ReducePrompt), ct).ConfigureAwait(false);

            var nextVersion = (await db.MeetingMinutes.Where(x => x.MeetingId == item.MeetingId).MaxAsync(x => (int?)x.Version, ct).ConfigureAwait(false) ?? 0) + 1;
            var now = clock.GetUtcNow();
            db.MeetingMinutes.Add(new MeetingMinutes
            {
                Id = Guid.NewGuid(),
                MeetingId = item.MeetingId,
                Version = nextVersion,
                ScenarioCode = item.ScenarioCode,
                Markdown = markdown,
                Model = generator.ModelName,
                GeneratedAtUtc = now,
                GeneratedBy = item.Actor
            });
            meeting.Status = MeetingStatus.Done;
            meeting.StatusMessage = null;
            meeting.UpdatedBy = item.Actor;
            meeting.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            logger.LogInformation("[MTG] 會議 {MeetingId} 紀要 v{Version} 產生完成。", item.MeetingId, nextVersion);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // 主機關閉：交由啟動時的回復流程處理
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
        {
            // 這兩類例外的訊息皆由本模組撰寫（不含逐字稿或模型內容），可直接顯示給使用者
            logger.LogWarning(ex, "[MTG] 會議 {MeetingId} 紀要產生失敗。", item.MeetingId);
            await MarkFailedAsync(db, meeting, item.Actor, ex.Message).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[MTG] 會議 {MeetingId} 紀要產生發生未預期錯誤。", item.MeetingId);
            await MarkFailedAsync(db, meeting, item.Actor, "紀要產生發生未預期錯誤，請稍後重試或聯絡管理員。").ConfigureAwait(false);
        }
    }

    private async Task MarkFailedAsync(MTGDbContext db, Meeting meeting, string actor, string message)
    {
        meeting.Status = MeetingStatus.Failed;
        meeting.StatusMessage = message.Length > 500 ? message[..500] : message;
        meeting.UpdatedBy = actor;
        meeting.UpdatedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
