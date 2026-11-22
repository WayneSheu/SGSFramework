using Microsoft.EntityFrameworkCore;
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
/// 會議錄音：上傳到檔案管理（FMS，分類 <c>meeting-audio</c>，AES-256-GCM 加密、非通用 API，只經 MTG 授權後存取），
/// 再放入轉錄佇列由背景工作者轉成逐字稿。MTG 只保存關聯與轉錄狀態，不保存音檔本身。
/// </summary>
public sealed class MeetingAudioService(
    MTGDbContext db,
    IFileManagementService files,
    TranscriptionQueue queue,
    IOptionsMonitor<MtgOptions> options,
    TimeProvider clock,
    ILogger<MeetingAudioService> logger)
{
    public const string FileCategory = "meeting-audio";
    public const string ReferenceType = "Meeting";

    public async Task<MtgResult<IReadOnlyList<MeetingAudioDto>>> ListAsync(Guid meetingId, CancellationToken ct)
    {
        if (!await db.Meetings.AnyAsync(m => m.Id == meetingId, ct).ConfigureAwait(false))
            return MtgResult<IReadOnlyList<MeetingAudioDto>>.Fail(MtgErrors.NotFound("會議"));

        var items = await db.MeetingAudios.AsNoTracking().Where(a => a.MeetingId == meetingId).OrderByDescending(a => a.CreatedAtUtc)
            .Select(a => ToDto(a)).ToListAsync(ct).ConfigureAwait(false);
        return MtgResult<IReadOnlyList<MeetingAudioDto>>.Ok(items);
    }

    public async Task<MtgResult<MeetingAudioDto>> UploadAsync(
        string actor, Guid meetingId, Stream content, string fileName, string? expectedSha256, bool autoSummarize, string? scenarioCode, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!options.CurrentValue.Whisper.Enabled) return MtgResult<MeetingAudioDto>.Fail(MtgErrors.WhisperDisabled());
        if (string.IsNullOrWhiteSpace(fileName)) return MtgResult<MeetingAudioDto>.Fail(MtgErrors.Validation("請選擇要上傳的音檔。"));

        var meeting = await db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId, ct).ConfigureAwait(false);
        if (meeting is null) return MtgResult<MeetingAudioDto>.Fail(MtgErrors.NotFound("會議"));
        if (MeetingService.IsBusy(meeting.Status)) return MtgResult<MeetingAudioDto>.Fail(MtgErrors.Conflict(BusyText(meeting.Status)));

        var scenario = string.IsNullOrWhiteSpace(scenarioCode) ? null : scenarioCode.Trim().ToLowerInvariant();
        if (autoSummarize && scenario is not null && !await db.PromptTemplates.AnyAsync(t => t.ScenarioCode == scenario && t.IsActive, ct).ConfigureAwait(false))
            return MtgResult<MeetingAudioDto>.Fail(MtgErrors.Validation($"找不到啟用中的場景「{scenario}」。"));

        FileMetadataDto stored;
        try
        {
            stored = await files.UploadAsync(
                new FileUploadRequest(content, Path.GetFileName(fileName.Trim()), FileCategory, ReferenceType, meetingId.ToString(), expectedSha256),
                FileAccessScope.Internal, ct).ConfigureAwait(false);
        }
        catch (FileRejectedException ex)
        {
            return MtgResult<MeetingAudioDto>.Fail(MtgErrors.FileRejected(ex.StatusCode, ex.Message));
        }

        var previousStatus = meeting.Status;
        var previousMessage = meeting.StatusMessage;
        var now = clock.GetUtcNow();
        var audio = new MeetingAudio
        {
            Id = Guid.NewGuid(),
            MeetingId = meetingId,
            FileId = stored.Id,
            FileName = stored.OriginalFileName,
            ContentType = stored.ContentType,
            FileSize = stored.FileSize,
            Sha256 = stored.Sha256,
            Status = AudioStatus.Queued,
            AutoSummarize = autoSummarize,
            SummarizeScenarioCode = autoSummarize ? scenario : null,
            CreatedBy = actor,
            CreatedAtUtc = now
        };

        try
        {
            db.MeetingAudios.Add(audio);
            meeting.Status = MeetingStatus.Transcribing;
            meeting.StatusMessage = null;
            meeting.UpdatedBy = actor;
            meeting.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 補償：中繼資料寫入失敗時回收已寫入 FMS 的檔案
            db.ChangeTracker.Clear();
            await DeleteStoredQuietlyAsync(stored.Id).ConfigureAwait(false);
            throw;
        }

        if (!queue.TryEnqueue(new TranscriptionWorkItem(audio.Id, meetingId, actor)))
        {
            // 佇列已滿：整個上傳復原（音檔、紀錄與會議狀態），避免會議卡在「轉錄中」
            db.MeetingAudios.Remove(audio);
            meeting.Status = previousStatus;
            meeting.StatusMessage = previousMessage;
            await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            await DeleteStoredQuietlyAsync(stored.Id).ConfigureAwait(false);
            return MtgResult<MeetingAudioDto>.Fail(MtgErrors.TranscriptionQueueFull());
        }

        return MtgResult<MeetingAudioDto>.Ok(ToDto(audio));
    }

    /// <summary>重新轉錄：沿用已存放的音檔（轉錄成功後只取代這個音檔先前產生的片段）。</summary>
    public async Task<MtgResult<MeetingAudioDto>> RetranscribeAsync(string actor, Guid meetingId, Guid audioId, CancellationToken ct)
    {
        if (!options.CurrentValue.Whisper.Enabled) return MtgResult<MeetingAudioDto>.Fail(MtgErrors.WhisperDisabled());

        var meeting = await db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId, ct).ConfigureAwait(false);
        if (meeting is null) return MtgResult<MeetingAudioDto>.Fail(MtgErrors.NotFound("會議"));
        if (MeetingService.IsBusy(meeting.Status)) return MtgResult<MeetingAudioDto>.Fail(MtgErrors.Conflict(BusyText(meeting.Status)));

        var audio = await db.MeetingAudios.FirstOrDefaultAsync(a => a.Id == audioId && a.MeetingId == meetingId, ct).ConfigureAwait(false);
        if (audio is null) return MtgResult<MeetingAudioDto>.Fail(MtgErrors.NotFound("音檔"));
        if (audio.Status == AudioStatus.Expired) return MtgResult<MeetingAudioDto>.Fail(MtgErrors.Conflict("錄音已依保存期限刪除，無法重新轉錄。"));

        var previousMeetingStatus = meeting.Status;
        var previousMeetingMessage = meeting.StatusMessage;
        var previousAudioStatus = audio.Status;
        var previousAudioMessage = audio.StatusMessage;
        var now = clock.GetUtcNow();

        audio.Status = AudioStatus.Queued;
        audio.StatusMessage = null;
        audio.UpdatedBy = actor;
        audio.UpdatedAtUtc = now;
        meeting.Status = MeetingStatus.Transcribing;
        meeting.StatusMessage = null;
        meeting.UpdatedBy = actor;
        meeting.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        if (!queue.TryEnqueue(new TranscriptionWorkItem(audio.Id, meetingId, actor)))
        {
            audio.Status = previousAudioStatus;
            audio.StatusMessage = previousAudioMessage;
            meeting.Status = previousMeetingStatus;
            meeting.StatusMessage = previousMeetingMessage;
            await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            return MtgResult<MeetingAudioDto>.Fail(MtgErrors.TranscriptionQueueFull());
        }

        return MtgResult<MeetingAudioDto>.Ok(ToDto(audio));
    }

    /// <summary>刪除音檔：FMS 虛擬刪除（保留期滿後由 FMS 清除實體檔案）。<paramref name="removeTranscript"/> 為 true 時一併移除由此音檔轉出的片段。</summary>
    public async Task<MtgResult<bool>> DeleteAsync(string actor, Guid meetingId, Guid audioId, bool removeTranscript, CancellationToken ct)
    {
        var audio = await db.MeetingAudios.FirstOrDefaultAsync(a => a.Id == audioId && a.MeetingId == meetingId, ct).ConfigureAwait(false);
        if (audio is null) return MtgResult<bool>.Fail(MtgErrors.NotFound("音檔"));
        if (audio.Status is AudioStatus.Queued or AudioStatus.Transcribing)
            return MtgResult<bool>.Fail(MtgErrors.Conflict("音檔轉錄中，請等轉錄完成後再刪除。"));

        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        if (removeTranscript)
        {
            await db.TranscriptSegments.Where(s => s.MeetingId == meetingId && s.AudioId == audioId).ExecuteDeleteAsync(ct).ConfigureAwait(false);
            var meeting = await db.Meetings.FirstOrDefaultAsync(m => m.Id == meetingId, ct).ConfigureAwait(false);
            if (meeting is { Status: MeetingStatus.TranscriptReady } && !await db.TranscriptSegments.AnyAsync(s => s.MeetingId == meetingId, ct).ConfigureAwait(false))
            {
                meeting.Status = MeetingStatus.Draft;
                meeting.UpdatedBy = actor;
                meeting.UpdatedAtUtc = clock.GetUtcNow();
            }
        }
        else
        {
            // 保留逐字稿：片段不再指向已刪除的音檔
            await db.TranscriptSegments.Where(s => s.MeetingId == meetingId && s.AudioId == audioId)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.AudioId, (Guid?)null), ct).ConfigureAwait(false);
        }

        db.MeetingAudios.Remove(audio);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        await DeleteStoredQuietlyAsync(audio.FileId).ConfigureAwait(false);
        return MtgResult<bool>.Ok(true);
    }

    /// <summary>開啟音檔下載串流（記憶體內解密）；呼叫端負責 Dispose。</summary>
    public async Task<MtgResult<FileDownload>> OpenDownloadAsync(Guid meetingId, Guid audioId, CancellationToken ct)
    {
        var audio = await db.MeetingAudios.AsNoTracking().Where(a => a.Id == audioId && a.MeetingId == meetingId)
            .Select(a => new { a.FileId, a.Status }).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (audio is null) return MtgResult<FileDownload>.Fail(MtgErrors.NotFound("音檔"));
        if (audio.Status == AudioStatus.Expired) return MtgResult<FileDownload>.Fail(MtgErrors.Gone("錄音已依保存期限刪除；逐字稿與紀要仍保留。"));
        var fileId = (Guid?)audio.FileId;

        try
        {
            var download = await files.OpenDownloadAsync(fileId.Value, FileAccessScope.Internal, ct).ConfigureAwait(false);
            if (download?.Content is null) return MtgResult<FileDownload>.Fail(MtgErrors.NotFound("音檔（可能已被清除）"));
            return MtgResult<FileDownload>.Ok(download);
        }
        catch (FileRejectedException ex)
        {
            return MtgResult<FileDownload>.Fail(MtgErrors.FileRejected(ex.StatusCode, ex.Message));
        }
    }

    private static string BusyText(MeetingStatus status) =>
        status == MeetingStatus.Transcribing ? "音檔轉錄中，請等轉錄完成後再上傳。" : "紀要產生中，請等產生完成後再上傳。";

    private async Task DeleteStoredQuietlyAsync(Guid fileId)
    {
        try
        {
            await files.DeleteAsync(fileId, FileAccessScope.Internal, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[MTG] 回收 FMS 檔案 {FileId} 失敗，請改由檔案管理處理。", fileId);
        }
    }

    internal static MeetingAudioDto ToDto(MeetingAudio a) => new(
        a.Id, a.FileName, a.ContentType, a.FileSize, a.Status, a.StatusMessage, a.DurationSeconds, a.Language,
        a.SegmentCount, a.AutoSummarize, a.CreatedBy, a.CreatedAtUtc, a.TranscribedAtUtc, a.ExpiredAtUtc);
}
