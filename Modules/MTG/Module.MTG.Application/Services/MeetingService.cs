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

/// <summary>會議維護、逐字稿貼上／上傳（含術語校正）與紀要產生請求。</summary>
public sealed class MeetingService(
    MTGDbContext db,
    TimeProvider clock,
    IOptionsMonitor<MtgOptions> options,
    MinutesQueue queue,
    IFileManagementService files,
    ILogger<MeetingService> logger)
{
    public const int MaxTitleLength = 200;

    /// <summary>會議正在被背景工作者處理（轉錄或產生紀要）：此時不可修改、刪除或寫入逐字稿，避免與工作者互相覆蓋。</summary>
    public static bool IsBusy(MeetingStatus status) => status is MeetingStatus.Summarizing or MeetingStatus.Transcribing;

    private static string BusyMessage(MeetingStatus status) =>
        status == MeetingStatus.Transcribing ? "音檔轉錄中，請等轉錄完成後再操作。" : "紀要產生中，請等產生完成後再操作。";

    // ---------- 會議 ----------

    public async Task<PagedResult<MeetingDto>> SearchAsync(MeetingQueryRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);

        var query = db.Meetings.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(m => m.Title.Contains(keyword));
        }
        if (request.Status is { } status) query = query.Where(m => m.Status == status);
        if (!string.IsNullOrWhiteSpace(request.ScenarioCode)) query = query.Where(m => m.ScenarioCode == request.ScenarioCode);
        if (request.ProjectId is { } projectId) query = query.Where(m => m.ProjectId == projectId);

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(m => m.MeetingTimeUtc).ThenBy(m => m.Id)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Select(m => new MeetingDto(m.Id, m.Title, m.ScenarioCode, m.MeetingTimeUtc, m.ProjectId, m.Status, m.StatusMessage,
                m.Organizer, m.Segments.Count, m.MinutesVersions.Count, m.CreatedAtUtc, m.UpdatedAtUtc))
            .ToListAsync(ct).ConfigureAwait(false);

        return new PagedResult<MeetingDto>(items, total, pageIndex, pageSize);
    }

    public async Task<MtgResult<MeetingDto>> GetAsync(Guid id, CancellationToken ct)
    {
        var dto = await db.Meetings.AsNoTracking().Where(m => m.Id == id)
            .Select(m => new MeetingDto(m.Id, m.Title, m.ScenarioCode, m.MeetingTimeUtc, m.ProjectId, m.Status, m.StatusMessage,
                m.Organizer, m.Segments.Count, m.MinutesVersions.Count, m.CreatedAtUtc, m.UpdatedAtUtc))
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return dto is null ? MtgResult<MeetingDto>.Fail(MtgErrors.NotFound("會議")) : MtgResult<MeetingDto>.Ok(dto);
    }

    public async Task<MtgResult<MeetingDto>> CreateAsync(string actor, SaveMeetingRequest request, CancellationToken ct)
    {
        var validation = await ValidateSaveAsync(request, ct).ConfigureAwait(false);
        if (validation is not null) return MtgResult<MeetingDto>.Fail(validation);

        var now = clock.GetUtcNow();
        var meeting = new Meeting
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            ScenarioCode = NormalizeScenario(request.ScenarioCode),
            MeetingTimeUtc = request.MeetingTimeUtc ?? now,
            ProjectId = request.ProjectId,
            Status = MeetingStatus.Draft,
            Organizer = actor,
            CreatedBy = actor,
            CreatedAtUtc = now
        };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return await GetAsync(meeting.Id, ct).ConfigureAwait(false);
    }

    public async Task<MtgResult<MeetingDto>> UpdateAsync(string actor, Guid id, SaveMeetingRequest request, CancellationToken ct)
    {
        var validation = await ValidateSaveAsync(request, ct).ConfigureAwait(false);
        if (validation is not null) return MtgResult<MeetingDto>.Fail(validation);

        var meeting = await db.Meetings.FirstOrDefaultAsync(m => m.Id == id, ct).ConfigureAwait(false);
        if (meeting is null) return MtgResult<MeetingDto>.Fail(MtgErrors.NotFound("會議"));
        if (IsBusy(meeting.Status))
            return MtgResult<MeetingDto>.Fail(MtgErrors.Conflict(BusyMessage(meeting.Status)));

        meeting.Title = request.Title.Trim();
        meeting.ScenarioCode = NormalizeScenario(request.ScenarioCode);
        if (request.MeetingTimeUtc is { } time) meeting.MeetingTimeUtc = time;
        meeting.ProjectId = request.ProjectId;
        meeting.UpdatedBy = actor;
        meeting.UpdatedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return await GetAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<MtgResult<bool>> DeleteAsync(Guid id, CancellationToken ct)
    {
        var meeting = await db.Meetings.FirstOrDefaultAsync(m => m.Id == id, ct).ConfigureAwait(false);
        if (meeting is null) return MtgResult<bool>.Fail(MtgErrors.NotFound("會議"));
        if (IsBusy(meeting.Status))
            return MtgResult<bool>.Fail(MtgErrors.Conflict(BusyMessage(meeting.Status)));

        var fileIds = await db.MeetingAudios.Where(a => a.MeetingId == id).Select(a => a.FileId).ToListAsync(ct).ConfigureAwait(false);

        db.Meetings.Remove(meeting); // 逐字稿、紀要版本與音檔紀錄由資料庫串聯刪除
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        // 錄音在 FMS 虛擬刪除（實體檔案由 FMS 排程於保留期滿後清除）；失敗只記錄，不讓使用者的刪除動作失敗
        foreach (var fileId in fileIds)
        {
            try
            {
                await files.DeleteAsync(fileId, FileAccessScope.Internal, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "[MTG] 刪除會議 {MeetingId} 的音檔 {FileId}（FMS）失敗，請改由檔案管理處理。", id, fileId);
            }
        }

        return MtgResult<bool>.Ok(true);
    }

    // ---------- 逐字稿 ----------

    public async Task<MtgResult<PasteTranscriptResult>> PasteTranscriptAsync(string actor, Guid id, PasteTranscriptRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var max = options.CurrentValue.MaxTranscriptChars;
        if (string.IsNullOrWhiteSpace(request.Text)) return MtgResult<PasteTranscriptResult>.Fail(MtgErrors.Validation("逐字稿內容不得為空。"));
        if (request.Text.Length > max) return MtgResult<PasteTranscriptResult>.Fail(MtgErrors.Validation($"逐字稿不得超過 {max:N0} 字元。"));

        var meeting = await db.Meetings.FirstOrDefaultAsync(m => m.Id == id, ct).ConfigureAwait(false);
        if (meeting is null) return MtgResult<PasteTranscriptResult>.Fail(MtgErrors.NotFound("會議"));
        if (IsBusy(meeting.Status))
            return MtgResult<PasteTranscriptResult>.Fail(MtgErrors.Conflict(BusyMessage(meeting.Status)));

        var parsed = TranscriptParser.Parse(request.Text, 1);
        if (parsed.Count == 0) return MtgResult<PasteTranscriptResult>.Fail(MtgErrors.Validation("逐字稿內容不得為空。"));

        // 覆蓋時的刪除與新增放在同一交易：新增失敗不會讓原逐字稿消失，也避免 (MeetingId, Seq) 唯一索引在同批次中衝突
        await using var tx = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var startSeq = 1;
        if (request.Replace)
        {
            await db.TranscriptSegments.Where(s => s.MeetingId == id).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        }
        else
        {
            startSeq = (await db.TranscriptSegments.Where(s => s.MeetingId == id).MaxAsync(s => (int?)s.Seq, ct).ConfigureAwait(false) ?? 0) + 1;
            parsed = TranscriptParser.Parse(request.Text, startSeq);
        }

        var terms = await LoadGlossaryAsync(ct).ConfigureAwait(false);
        var (built, corrected) = SegmentRules.Build(
            id, startSeq, parsed.Select(p => new NewSegment(null, p.Speaker, null, null, p.Text)), terms, audioId: null);
        db.TranscriptSegments.AddRange(built);

        meeting.Status = MeetingStatus.TranscriptReady;
        meeting.StatusMessage = null;
        meeting.UpdatedBy = actor;
        meeting.UpdatedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);

        var total = await db.TranscriptSegments.CountAsync(s => s.MeetingId == id, ct).ConfigureAwait(false);
        return MtgResult<PasteTranscriptResult>.Ok(new PasteTranscriptResult(parsed.Count, total, corrected));
    }

    // ---------- 桌面端同步 ----------

    /// <summary>
    /// 桌面端批次上傳逐字稿片段：依 clientSegmentId 冪等（已存在的略過，可安全重送），附加在既有片段之後並套用術語校正。
    /// 並發的兩個批次可能搶到同一序號，唯一索引會擋下其中一個，此時回 409 請桌面端重送。
    /// </summary>
    public async Task<MtgResult<UploadSegmentsResult>> AddSegmentsAsync(string actor, Guid id, UploadSegmentsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (items, error) = SegmentRules.NormalizeClientBatch(
            request.Segments?.Select(s => new NewSegment(s.ClientSegmentId, s.Speaker, s.StartSeconds, s.EndSeconds, s.Text)).ToList());
        if (error is not null) return MtgResult<UploadSegmentsResult>.Fail(MtgErrors.Validation(error));

        var meeting = await db.Meetings.FirstOrDefaultAsync(m => m.Id == id, ct).ConfigureAwait(false);
        if (meeting is null) return MtgResult<UploadSegmentsResult>.Fail(MtgErrors.NotFound("會議"));
        if (IsBusy(meeting.Status)) return MtgResult<UploadSegmentsResult>.Fail(MtgErrors.Conflict(BusyMessage(meeting.Status)));

        var max = options.CurrentValue.MaxTranscriptChars;
        var existingChars = await db.TranscriptSegments.Where(s => s.MeetingId == id).SumAsync(s => (long?)s.Text.Length, ct).ConfigureAwait(false) ?? 0;
        if (existingChars + items.Sum(i => (long)i.Text.Length) > max)
            return MtgResult<UploadSegmentsResult>.Fail(MtgErrors.Validation($"逐字稿總長度不得超過 {max:N0} 字元。"));

        var ids = items.Select(i => i.ClientSegmentId!).ToList();
        var known = (await db.TranscriptSegments.Where(s => s.MeetingId == id && s.ClientSegmentId != null && ids.Contains(s.ClientSegmentId))
            .Select(s => s.ClientSegmentId!).ToListAsync(ct).ConfigureAwait(false)).ToHashSet(StringComparer.Ordinal);
        var fresh = items.Where(i => !known.Contains(i.ClientSegmentId!)).ToList();

        if (fresh.Count > 0)
        {
            var startSeq = (await db.TranscriptSegments.Where(s => s.MeetingId == id).MaxAsync(s => (int?)s.Seq, ct).ConfigureAwait(false) ?? 0) + 1;
            var terms = await LoadGlossaryAsync(ct).ConfigureAwait(false);
            var (built, _) = SegmentRules.Build(id, startSeq, fresh, terms, audioId: null);
            db.TranscriptSegments.AddRange(built);

            meeting.Status = MeetingStatus.TranscriptReady;
            meeting.StatusMessage = null;
            meeting.UpdatedBy = actor;
            meeting.UpdatedAtUtc = clock.GetUtcNow();
            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch (DbUpdateException ex)
            {
                db.ChangeTracker.Clear();
                logger.LogWarning(ex, "[MTG] 會議 {MeetingId} 寫入桌面端片段時發生衝突（可能是並發批次）。", id);
                return MtgResult<UploadSegmentsResult>.Fail(MtgErrors.Conflict("同時有其他批次正在寫入此會議的逐字稿，請稍後重送。"));
            }
        }

        var total = await db.TranscriptSegments.CountAsync(s => s.MeetingId == id, ct).ConfigureAwait(false);
        return MtgResult<UploadSegmentsResult>.Ok(new UploadSegmentsResult(fresh.Count, items.Count - fresh.Count, total));
    }

    /// <summary>桌面端結束會議：確認已有逐字稿，並依要求接著產生紀要（沿用 <see cref="RequestMinutesAsync"/> 的所有檢查）。</summary>
    public async Task<MtgResult<MeetingDto>> FinalizeAsync(string actor, Guid id, FinalizeMeetingRequest? request, CancellationToken ct)
    {
        var meeting = await db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct).ConfigureAwait(false);
        if (meeting is null) return MtgResult<MeetingDto>.Fail(MtgErrors.NotFound("會議"));
        if (!await db.TranscriptSegments.AnyAsync(s => s.MeetingId == id, ct).ConfigureAwait(false))
            return MtgResult<MeetingDto>.Fail(MtgErrors.Validation("尚無逐字稿，無法結束會議。"));

        if (request?.GenerateMinutes == false) return await GetAsync(id, ct).ConfigureAwait(false);
        return await RequestMinutesAsync(actor, id, new GenerateMinutesRequest(request?.ScenarioCode), ct).ConfigureAwait(false);
    }

    public async Task<MtgResult<IReadOnlyList<TranscriptSegmentDto>>> GetTranscriptAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Meetings.AnyAsync(m => m.Id == id, ct).ConfigureAwait(false))
            return MtgResult<IReadOnlyList<TranscriptSegmentDto>>.Fail(MtgErrors.NotFound("會議"));

        var items = await db.TranscriptSegments.AsNoTracking().Where(s => s.MeetingId == id).OrderBy(s => s.Seq)
            .Select(s => new TranscriptSegmentDto(s.Seq, s.Speaker, s.StartSeconds, s.EndSeconds, s.Text, s.CorrectedText))
            .ToListAsync(ct).ConfigureAwait(false);
        return MtgResult<IReadOnlyList<TranscriptSegmentDto>>.Ok(items);
    }

    // ---------- 紀要 ----------

    /// <summary>要求產生紀要：標記為產生中並放入佇列，由背景工作者逐一處理。</summary>
    public async Task<MtgResult<MeetingDto>> RequestMinutesAsync(string actor, Guid id, GenerateMinutesRequest? request, CancellationToken ct)
    {
        if (!options.CurrentValue.Enabled) return MtgResult<MeetingDto>.Fail(MtgErrors.Disabled());

        var meeting = await db.Meetings.FirstOrDefaultAsync(m => m.Id == id, ct).ConfigureAwait(false);
        if (meeting is null) return MtgResult<MeetingDto>.Fail(MtgErrors.NotFound("會議"));
        if (IsBusy(meeting.Status))
            return MtgResult<MeetingDto>.Fail(MtgErrors.Conflict(meeting.Status == MeetingStatus.Transcribing ? "音檔轉錄中，轉錄完成後才能產生紀要。" : "此會議的紀要正在產生中。"));
        if (!await db.TranscriptSegments.AnyAsync(s => s.MeetingId == id, ct).ConfigureAwait(false))
            return MtgResult<MeetingDto>.Fail(MtgErrors.Validation("尚無逐字稿，請先貼上或上傳逐字稿。"));

        var scenario = NormalizeScenario(request?.ScenarioCode ?? meeting.ScenarioCode);
        var template = await db.PromptTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.ScenarioCode == scenario, ct).ConfigureAwait(false);
        if (template is null) return MtgResult<MeetingDto>.Fail(MtgErrors.Validation($"找不到場景「{scenario}」的模板。"));
        if (!template.IsActive) return MtgResult<MeetingDto>.Fail(MtgErrors.Validation($"場景「{template.DisplayName}」已停用。"));

        var previousStatus = meeting.Status;
        var previousMessage = meeting.StatusMessage;
        meeting.Status = MeetingStatus.Summarizing;
        meeting.StatusMessage = null;
        meeting.UpdatedBy = actor;
        meeting.UpdatedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        if (!queue.TryEnqueue(new MinutesWorkItem(id, scenario, actor)))
        {
            // 佇列已滿：還原狀態，避免會議永遠卡在「產生中」
            meeting.Status = previousStatus;
            meeting.StatusMessage = previousMessage;
            await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            return MtgResult<MeetingDto>.Fail(MtgErrors.QueueFull());
        }

        return await GetAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<MtgResult<IReadOnlyList<MinutesSummaryDto>>> ListMinutesAsync(Guid id, CancellationToken ct)
    {
        if (!await db.Meetings.AnyAsync(m => m.Id == id, ct).ConfigureAwait(false))
            return MtgResult<IReadOnlyList<MinutesSummaryDto>>.Fail(MtgErrors.NotFound("會議"));

        var items = await db.MeetingMinutes.AsNoTracking().Where(x => x.MeetingId == id).OrderByDescending(x => x.Version)
            .Select(x => new MinutesSummaryDto(x.Version, x.ScenarioCode, x.Model, x.GeneratedAtUtc, x.GeneratedBy))
            .ToListAsync(ct).ConfigureAwait(false);
        return MtgResult<IReadOnlyList<MinutesSummaryDto>>.Ok(items);
    }

    /// <summary>取得紀要；未指定版本時回傳最新版。</summary>
    public async Task<MtgResult<MinutesDto>> GetMinutesAsync(Guid id, int? version, CancellationToken ct)
    {
        var query = db.MeetingMinutes.AsNoTracking().Where(x => x.MeetingId == id);
        query = version is { } v ? query.Where(x => x.Version == v) : query.OrderByDescending(x => x.Version);

        var minutes = await query.Select(x => new MinutesDto(x.Version, x.ScenarioCode, x.Markdown, x.Model, x.GeneratedAtUtc, x.GeneratedBy))
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        return minutes is null ? MtgResult<MinutesDto>.Fail(MtgErrors.NotFound("會議紀要")) : MtgResult<MinutesDto>.Ok(minutes);
    }

    // ---------- 內部 ----------

    private static string NormalizeScenario(string? code) =>
        string.IsNullOrWhiteSpace(code) ? ScenarioDefaults.General : code.Trim().ToLowerInvariant();

    private async Task<MtgError?> ValidateSaveAsync(SaveMeetingRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > MaxTitleLength)
            return MtgErrors.Validation($"會議標題為必填且不得超過 {MaxTitleLength} 字。");

        var scenario = NormalizeScenario(request.ScenarioCode);
        if (!ScenarioDefaults.IsValidCode(scenario)) return MtgErrors.Validation("場景代碼僅限小寫英數字與連字號。");
        if (!await db.PromptTemplates.AnyAsync(t => t.ScenarioCode == scenario, ct).ConfigureAwait(false))
            return MtgErrors.Validation($"找不到場景「{scenario}」。");
        return null;
    }

    private async Task<IReadOnlyList<GlossaryTerm>> LoadGlossaryAsync(CancellationToken ct)
    {
        var rows = await db.GlossaryEntries.AsNoTracking().Where(g => g.IsActive).Select(g => new { g.Term, g.Variants }).ToListAsync(ct).ConfigureAwait(false);
        return rows.Select(r => new GlossaryTerm(r.Term, GlossaryCorrector.ParseVariants(r.Variants))).ToList();
    }
}
