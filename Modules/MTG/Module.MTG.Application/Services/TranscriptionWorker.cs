using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGS.Modules.MTG.Application.Contracts;
using SGS.Modules.MTG.Application.Options;
using SGS.Modules.MTG.Application.Rules;
using SGS.Modules.MTG.Infrastructure.Dbcontexts;
using SGS.Modules.MTG.Infrastructure.Entities;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.MTG.Application.Services;

/// <summary>
/// 背景工作者：依序（一次一個，音檔整檔會讀入記憶體）處理轉錄佇列。
/// 流程：自 FMS 讀取並解密音檔（不落地）→ 以術語表產生提示詞 → 呼叫轉錄服務 → 套用術語校正 → 在同一交易取代此音檔先前的片段並寫入新片段
/// → 會議回到「逐字稿就緒」→ 若上傳時勾選自動產生紀要，接著要求產生紀要。
/// 失敗只記錄安全訊息，不含音檔或逐字稿內容。
/// </summary>
public sealed class TranscriptionWorker(
    TranscriptionQueue queue,
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<MtgOptions> options,
    TimeProvider clock,
    ILogger<TranscriptionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverInterruptedAsync(stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            TranscriptionWorkItem item;
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
                logger.LogError(ex, "[MTG] 轉錄作業（音檔 {AudioId}）發生未預期例外。", item.AudioId);
            }
        }
    }

    /// <summary>服務重啟時，佇列內容已遺失；仍在排隊或轉錄中的音檔標為失敗，使用者可直接「重新轉錄」（音檔仍在 FMS）。</summary>
    private async Task RecoverInterruptedAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MTGDbContext>();
            var now = clock.GetUtcNow();

            var audios = await db.MeetingAudios.Where(a => a.Status == AudioStatus.Queued || a.Status == AudioStatus.Transcribing).ToListAsync(ct).ConfigureAwait(false);
            foreach (var audio in audios)
            {
                audio.Status = AudioStatus.Failed;
                audio.StatusMessage = "服務重新啟動，轉錄中斷，請重新轉錄。";
                audio.UpdatedAtUtc = now;
            }

            var meetings = await db.Meetings.Where(m => m.Status == MeetingStatus.Transcribing).ToListAsync(ct).ConfigureAwait(false);
            foreach (var meeting in meetings)
            {
                meeting.Status = MeetingStatus.Failed;
                meeting.StatusMessage = "服務重新啟動，音檔轉錄中斷，請在音檔清單按「重新轉錄」。";
                meeting.UpdatedAtUtc = now;
            }

            if (audios.Count > 0 || meetings.Count > 0)
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                logger.LogWarning("[MTG] 已將 {Audios} 個中斷的音檔轉錄與 {Meetings} 場會議標記為失敗。", audios.Count, meetings.Count);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 資料庫尚未就緒等情況不應讓整個主機啟動失敗
            logger.LogWarning(ex, "[MTG] 啟動時回復中斷的轉錄作業失敗。");
        }
    }

    private async Task ProcessAsync(TranscriptionWorkItem item, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<MTGDbContext>();

        var audio = await db.MeetingAudios.FirstOrDefaultAsync(a => a.Id == item.AudioId, ct).ConfigureAwait(false);
        var meeting = await db.Meetings.FirstOrDefaultAsync(m => m.Id == item.MeetingId, ct).ConfigureAwait(false);
        if (audio is null || meeting is null) return; // 排隊期間會議或音檔已被刪除

        audio.Status = AudioStatus.Transcribing;
        audio.StatusMessage = null;
        audio.UpdatedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        try
        {
            var files = sp.GetRequiredService<IFileManagementService>();
            var engine = sp.GetRequiredService<ITranscriptionEngine>();
            var maxBytes = options.CurrentValue.Whisper.MaxAudioBytes;

            // 1. 自 FMS 讀取（記憶體內解密）並整檔讀入記憶體；有大小上限，不落地暫存檔
            using var buffer = new MemoryStream();
            var download = await files.OpenDownloadAsync(audio.FileId, FileAccessScope.Internal, ct).ConfigureAwait(false)
                           ?? throw new InvalidOperationException("音檔已不存在於檔案管理，無法轉錄。");
            if (download.Content is null) throw new InvalidOperationException("無法讀取音檔內容，請檢查 FMS 分類 meeting-audio 的設定。");
            await using (download.Content.ConfigureAwait(false))
            {
                await CopyWithLimitAsync(download.Content, buffer, maxBytes, ct).ConfigureAwait(false);
            }
            buffer.Position = 0;

            // 2. 術語表 → 提示詞（引導中英夾雜與專有名詞的辨識）與事後校正
            var terms = await LoadTermsAsync(db, ct).ConfigureAwait(false);
            var prompt = GlossaryCorrector.BuildInitialPrompt(terms, traditionalChinese: GlossaryCorrector.IsChineseOrAuto(options.CurrentValue.Whisper.Language));

            // 3. 轉錄
            var result = await engine.TranscribeAsync(buffer, audio.FileName, string.IsNullOrEmpty(prompt) ? null : prompt, ct).ConfigureAwait(false);
            if (result.Segments.Count == 0) throw new InvalidOperationException("轉錄結果是空的（音檔可能是靜音，或內容無法辨識）。");

            // 4. 同一交易：取代此音檔先前的片段、寫入新片段、更新音檔與會議狀態
            await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
            await db.TranscriptSegments.Where(s => s.MeetingId == meeting.Id && s.AudioId == audio.Id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
            var startSeq = (await db.TranscriptSegments.Where(s => s.MeetingId == meeting.Id).MaxAsync(s => (int?)s.Seq, ct).ConfigureAwait(false) ?? 0) + 1;
            var (segments, _) = SegmentRules.Build(
                meeting.Id, startSeq,
                result.Segments.Select(s => new NewSegment(null, null, s.StartSeconds, s.EndSeconds, s.Text)),
                terms, audio.Id);
            db.TranscriptSegments.AddRange(segments);

            var now = clock.GetUtcNow();
            audio.Status = AudioStatus.Transcribed;
            audio.StatusMessage = null;
            audio.SegmentCount = segments.Count;
            audio.DurationSeconds = result.DurationSeconds ?? result.Segments.Max(s => s.EndSeconds);
            audio.Language = result.Language;
            audio.TranscribedAtUtc = now;
            audio.UpdatedAtUtc = now;
            meeting.Status = MeetingStatus.TranscriptReady;
            meeting.StatusMessage = null;
            meeting.UpdatedBy = item.Actor;
            meeting.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            await tx.CommitAsync(ct).ConfigureAwait(false);
            logger.LogInformation("[MTG] 音檔 {AudioId} 轉錄完成：{Count} 個片段。", audio.Id, segments.Count);

            // 5. 勾選了「轉錄後自動產生紀要」：接續要求產生（失敗不影響轉錄結果，使用者可手動產生）
            if (audio.AutoSummarize) await TryRequestMinutesAsync(sp, item, audio, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // 主機關閉：交由啟動時的回復流程處理
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
        {
            // 這兩類例外的訊息皆由本模組撰寫（不含音檔或逐字稿內容），可直接顯示給使用者
            logger.LogWarning(ex, "[MTG] 音檔 {AudioId} 轉錄失敗。", item.AudioId);
            await MarkFailedAsync(db, audio, meeting, item.Actor, ex.Message).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[MTG] 音檔 {AudioId} 轉錄發生未預期錯誤。", item.AudioId);
            await MarkFailedAsync(db, audio, meeting, item.Actor, "轉錄發生未預期錯誤，請稍後重試或聯絡管理員。").ConfigureAwait(false);
        }
    }

    private async Task TryRequestMinutesAsync(IServiceProvider sp, TranscriptionWorkItem item, MeetingAudio audio, CancellationToken ct)
    {
        try
        {
            var meetings = sp.GetRequiredService<MeetingService>();
            var result = await meetings.RequestMinutesAsync(item.Actor, item.MeetingId, new GenerateMinutesRequest(audio.SummarizeScenarioCode), ct).ConfigureAwait(false);
            if (!result.IsSuccess)
                logger.LogWarning("[MTG] 會議 {MeetingId} 轉錄後自動產生紀要被拒絕：{Code}。", item.MeetingId, result.Error?.Code);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[MTG] 會議 {MeetingId} 轉錄後自動產生紀要失敗。", item.MeetingId);
        }
    }

    private async Task MarkFailedAsync(MTGDbContext db, MeetingAudio audio, Meeting meeting, string actor, string message)
    {
        db.ChangeTracker.Clear(); // 丟棄失敗交易中未儲存的變更，重新載入再標記
        var freshAudio = await db.MeetingAudios.FirstOrDefaultAsync(a => a.Id == audio.Id).ConfigureAwait(false);
        var freshMeeting = await db.Meetings.FirstOrDefaultAsync(m => m.Id == meeting.Id).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        var text = message.Length > 500 ? message[..500] : message;

        if (freshAudio is not null)
        {
            freshAudio.Status = AudioStatus.Failed;
            freshAudio.StatusMessage = text;
            freshAudio.UpdatedAtUtc = now;
        }

        if (freshMeeting is not null)
        {
            freshMeeting.Status = MeetingStatus.Failed;
            freshMeeting.StatusMessage = $"音檔轉錄失敗：{text}";
            freshMeeting.UpdatedBy = actor;
            freshMeeting.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<GlossaryTerm>> LoadTermsAsync(MTGDbContext db, CancellationToken ct)
    {
        var rows = await db.GlossaryEntries.AsNoTracking().Where(g => g.IsActive).Select(g => new { g.Term, g.Variants }).ToListAsync(ct).ConfigureAwait(false);
        return rows.Select(r => new GlossaryTerm(r.Term, GlossaryCorrector.ParseVariants(r.Variants))).ToList();
    }

    /// <summary>複製串流並檢查上限，超過即中止（避免異常大的檔案耗盡記憶體）。</summary>
    public static async Task CopyWithLimitAsync(Stream source, Stream destination, long maxBytes, CancellationToken ct)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > maxBytes) throw new InvalidOperationException($"音檔超過轉錄可處理的大小上限（{maxBytes / 1024 / 1024} MB）。");
            await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
        }
    }
}
