using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.MTG.Application.Contracts;
using SGS.Modules.MTG.Application.Rules;
using SGS.Modules.MTG.Infrastructure.Dbcontexts;
using SGS.Modules.MTG.Infrastructure.Entities;
using SGSFramework.Core.Abstractions.Notifications;

namespace SGS.Modules.MTG.Application.Services;

/// <summary>
/// 會議待辦：由紀要抽取（先解析紀要裡的待辦表格，沒有表格才請模型輸出 JSON）、人工維護、跨會議查詢，以及郵件通知負責人。
/// <para>
/// 重新抽取（例如重新產生紀要後）只會取代「還沒被人碰過」的抽取結果——曾被編輯、已通知或狀態不是 Open 的待辦一律保留，
/// 內容相同的新待辦也不會重複加入，所以人工整理的成果不會被覆蓋。
/// </para>
/// </summary>
public sealed class ActionItemService(
    MTGDbContext db,
    TimeProvider clock,
    ITextGenerator generator,
    ILogger<ActionItemService> logger,
    IMailSender? mailSender = null)
{
    public const int MaxItemsPerMeeting = 100;
    public const int MaxExtractedItems = 50;
    public const int MaxExtraRecipients = 10;

    // ---------- 查詢 ----------

    public async Task<MtgResult<IReadOnlyList<ActionItemDto>>> ListAsync(Guid meetingId, CancellationToken ct)
    {
        if (!await db.Meetings.AnyAsync(m => m.Id == meetingId, ct).ConfigureAwait(false))
            return MtgResult<IReadOnlyList<ActionItemDto>>.Fail(MtgErrors.NotFound("會議"));

        var items = await db.ActionItems.AsNoTracking().Where(a => a.MeetingId == meetingId).OrderBy(a => a.Seq).ToListAsync(ct).ConfigureAwait(false);
        return MtgResult<IReadOnlyList<ActionItemDto>>.Ok(items.Select(ToDto).ToList());
    }

    public async Task<PagedResult<ActionItemListDto>> SearchAsync(ActionItemQueryRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var pageIndex = Math.Max(1, request.PageIndex);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var today = ActionItemRules.ReferenceDate(clock.GetUtcNow());

        var query = db.ActionItems.AsNoTracking().AsQueryable();
        if (request.MeetingId is { } meetingId) query = query.Where(a => a.MeetingId == meetingId);
        if (request.Status is { } status) query = query.Where(a => a.Status == status);
        if (request.OverdueOnly)
            query = query.Where(a => a.DueDate != null && a.DueDate < today && (a.Status == ActionItemStatus.Open || a.Status == ActionItemStatus.InProgress));
        if (request.DueFrom is { } from) query = query.Where(a => a.DueDate != null && a.DueDate >= from);
        if (request.DueTo is { } to) query = query.Where(a => a.DueDate != null && a.DueDate <= to);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(a => a.Description.Contains(keyword) || (a.Owner != null && a.Owner.Contains(keyword)));
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var rows = await query
            .OrderBy(a => a.DueDate == null).ThenBy(a => a.DueDate).ThenBy(a => a.CreatedAtUtc).ThenBy(a => a.Id)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .Select(a => new { a.Id, a.MeetingId, MeetingTitle = a.Meeting!.Title, a.Seq, a.Description, a.Owner, a.DueDate, a.DueText, a.Status, a.NotifiedAtUtc })
            .ToListAsync(ct).ConfigureAwait(false);

        var items = rows.Select(r => new ActionItemListDto(r.Id, r.MeetingId, r.MeetingTitle, r.Seq, r.Description, r.Owner, r.DueDate, r.DueText, r.Status,
            IsOverdue(r.DueDate, r.Status, today), r.NotifiedAtUtc)).ToList();
        return new PagedResult<ActionItemListDto>(items, total, pageIndex, pageSize);
    }

    public static bool IsOverdue(DateOnly? due, ActionItemStatus status, DateOnly today) =>
        due is { } d && d < today && status is ActionItemStatus.Open or ActionItemStatus.InProgress;

    // ---------- 人工維護 ----------

    public async Task<MtgResult<ActionItemDto>> CreateAsync(string actor, Guid meetingId, SaveActionItemRequest request, CancellationToken ct)
    {
        var invalid = Validate(request, out var clean);
        if (invalid is not null) return MtgResult<ActionItemDto>.Fail(invalid);
        if (!await db.Meetings.AnyAsync(m => m.Id == meetingId, ct).ConfigureAwait(false))
            return MtgResult<ActionItemDto>.Fail(MtgErrors.NotFound("會議"));

        var count = await db.ActionItems.CountAsync(a => a.MeetingId == meetingId, ct).ConfigureAwait(false);
        if (count >= MaxItemsPerMeeting) return MtgResult<ActionItemDto>.Fail(MtgErrors.Validation($"每場會議最多 {MaxItemsPerMeeting} 筆待辦。"));

        var seq = (await db.ActionItems.Where(a => a.MeetingId == meetingId).MaxAsync(a => (int?)a.Seq, ct).ConfigureAwait(false) ?? 0) + 1;
        var item = new ActionItem
        {
            Id = Guid.NewGuid(),
            MeetingId = meetingId,
            Seq = seq,
            Description = clean.Description,
            Owner = clean.Owner,
            OwnerEmail = clean.OwnerEmail,
            DueDate = request.DueDate,
            DueText = clean.DueText,
            Status = request.Status ?? ActionItemStatus.Open,
            Note = clean.Note,
            Source = ActionItemSource.Manual,
            CreatedBy = actor,
            CreatedAtUtc = clock.GetUtcNow()
        };
        db.ActionItems.Add(item);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return MtgResult<ActionItemDto>.Ok(ToDto(item));
    }

    public async Task<MtgResult<ActionItemDto>> UpdateAsync(string actor, Guid meetingId, Guid itemId, SaveActionItemRequest request, CancellationToken ct)
    {
        var invalid = Validate(request, out var clean);
        if (invalid is not null) return MtgResult<ActionItemDto>.Fail(invalid);

        var item = await db.ActionItems.FirstOrDefaultAsync(a => a.Id == itemId && a.MeetingId == meetingId, ct).ConfigureAwait(false);
        if (item is null) return MtgResult<ActionItemDto>.Fail(MtgErrors.NotFound("待辦事項"));

        item.Description = clean.Description;
        item.Owner = clean.Owner;
        item.OwnerEmail = clean.OwnerEmail;
        item.DueDate = request.DueDate;
        item.DueText = clean.DueText;
        item.Status = request.Status ?? item.Status;
        item.Note = clean.Note;
        item.UpdatedBy = actor;
        item.UpdatedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return MtgResult<ActionItemDto>.Ok(ToDto(item));
    }

    public async Task<MtgResult<bool>> DeleteAsync(Guid meetingId, Guid itemId, CancellationToken ct)
    {
        var item = await db.ActionItems.FirstOrDefaultAsync(a => a.Id == itemId && a.MeetingId == meetingId, ct).ConfigureAwait(false);
        if (item is null) return MtgResult<bool>.Fail(MtgErrors.NotFound("待辦事項"));

        db.ActionItems.Remove(item);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return MtgResult<bool>.Ok(true);
    }

    private sealed record CleanFields(string Description, string? Owner, string? OwnerEmail, string? DueText, string? Note);

    private static MtgError? Validate(SaveActionItemRequest? request, out CleanFields clean)
    {
        clean = new CleanFields(string.Empty, null, null, null, null);
        if (request is null) return MtgErrors.Validation("請提供待辦內容。");

        var description = ActionItemRules.CleanField(request.Description, ActionItemRules.MaxDescriptionLength);
        if (description is null) return MtgErrors.Validation("待辦事項內容不可空白。");
        if (request.Description.Trim().Length > ActionItemRules.MaxDescriptionLength) return MtgErrors.Validation($"待辦事項最多 {ActionItemRules.MaxDescriptionLength} 字。");
        if (request.Owner?.Trim().Length > ActionItemRules.MaxOwnerLength) return MtgErrors.Validation($"負責人最多 {ActionItemRules.MaxOwnerLength} 字。");

        string? email = null;
        if (!string.IsNullOrWhiteSpace(request.OwnerEmail))
        {
            if (!ActionItemRules.IsValidEmail(request.OwnerEmail, out var normalized)) return MtgErrors.Validation("負責人信箱格式不正確。");
            email = normalized;
        }

        if (request.Status is { } status && !Enum.IsDefined(status)) return MtgErrors.Validation("待辦狀態不正確。");

        clean = new CleanFields(description, ActionItemRules.CleanField(request.Owner, ActionItemRules.MaxOwnerLength), email,
            ActionItemRules.CleanField(request.DueText, ActionItemRules.MaxDueTextLength), ActionItemRules.CleanField(request.Note, ActionItemRules.MaxNoteLength));
        return null;
    }

    // ---------- 抽取 ----------

    /// <summary>從最新一版紀要抽取待辦並合併進目前清單（見類別說明的合併規則）。</summary>
    public async Task<MtgResult<ExtractActionItemsResult>> ExtractAsync(string actor, Guid meetingId, CancellationToken ct)
    {
        var meeting = await db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meetingId, ct).ConfigureAwait(false);
        if (meeting is null) return MtgResult<ExtractActionItemsResult>.Fail(MtgErrors.NotFound("會議"));
        if (MeetingService.IsBusy(meeting.Status))
            return MtgResult<ExtractActionItemsResult>.Fail(MtgErrors.Conflict("會議處理中，請等處理完成後再抽取待辦。"));

        var minutes = await db.MeetingMinutes.AsNoTracking().Where(x => x.MeetingId == meetingId).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (minutes is null) return MtgResult<ExtractActionItemsResult>.Fail(MtgErrors.Conflict("這場會議還沒有紀要，請先產生紀要再抽取待辦。"));

        var reference = ActionItemRules.ReferenceDate(meeting.MeetingTimeUtc);
        var method = "table";
        var extracted = ActionItemRules.ParseTable(minutes.Markdown, reference);

        if (extracted.Count == 0)
        {
            method = "llm";
            try
            {
                var raw = await generator.GenerateAsync(ActionItemRules.BuildExtractionPrompt(minutes.Markdown), ct).ConfigureAwait(false);
                extracted = ActionItemRules.ParseJson(raw, reference);
            }
            catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
            {
                // 這兩類例外的訊息由本模組撰寫（不含紀要內容），可顯示給使用者
                logger.LogWarning(ex, "[MTG] 會議 {MeetingId} 待辦抽取失敗。", meetingId);
                return MtgResult<ExtractActionItemsResult>.Fail(new MtgError(503, "MTG_EXTRACT_FAILED", "待辦抽取失敗", ex.Message));
            }
        }

        var result = await MergeAsync(actor, meetingId, minutes.Version, extracted.Take(MaxExtractedItems).ToList(), method, ct).ConfigureAwait(false);
        return MtgResult<ExtractActionItemsResult>.Ok(result);
    }

    private async Task<ExtractActionItemsResult> MergeAsync(string actor, Guid meetingId, int minutesVersion, IReadOnlyList<ExtractedActionItem> extracted, string method, CancellationToken ct)
    {
        var existing = await db.ActionItems.Where(a => a.MeetingId == meetingId).OrderBy(a => a.Seq).ToListAsync(ct).ConfigureAwait(false);

        // 沒被人碰過的抽取結果可以換新；其餘一律保留
        var replaceable = existing.Where(a => a.Source == ActionItemSource.Extracted && a.UpdatedAtUtc is null && a.NotifiedAtUtc is null && a.Status == ActionItemStatus.Open).ToList();
        db.ActionItems.RemoveRange(replaceable);
        var kept = existing.Except(replaceable).ToList();

        var seen = kept.Select(a => ActionItemRules.NormalizeKey(a.Description)).ToHashSet();
        var seq = kept.Count == 0 ? 0 : kept.Max(a => a.Seq);
        var now = clock.GetUtcNow();
        var added = 0;

        foreach (var e in extracted)
        {
            if (kept.Count + added >= MaxItemsPerMeeting) break;
            if (!seen.Add(ActionItemRules.NormalizeKey(e.Description))) continue;

            db.ActionItems.Add(new ActionItem
            {
                Id = Guid.NewGuid(),
                MeetingId = meetingId,
                Seq = ++seq,
                Description = e.Description,
                Owner = e.Owner,
                DueDate = e.DueDate,
                DueText = e.DueText,
                Note = e.Note,
                Status = ActionItemStatus.Open,
                Source = ActionItemSource.Extracted,
                MinutesVersion = minutesVersion,
                CreatedBy = actor,
                CreatedAtUtc = now
            });
            added++;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return new ExtractActionItemsResult(added, replaceable.Count, kept.Count, kept.Count + added, method);
    }

    // ---------- 通知 ----------

    /// <summary>
    /// 郵件通知待辦：有負責人信箱的待辦各寄一封給該負責人（同一人的合併成一封）；另可給額外收件人一封完整清單。
    /// 所有動態文字皆 HTML 編碼；收件人逐一驗證格式；只有寄成功的待辦才會記錄通知時間。
    /// </summary>
    public async Task<MtgResult<NotifyActionItemsResult>> NotifyAsync(string actor, Guid meetingId, NotifyActionItemsRequest? request, CancellationToken ct)
    {
        if (mailSender is null) return MtgResult<NotifyActionItemsResult>.Fail(MtgErrors.MailUnavailable());

        var meeting = await db.Meetings.AsNoTracking().FirstOrDefaultAsync(m => m.Id == meetingId, ct).ConfigureAwait(false);
        if (meeting is null) return MtgResult<NotifyActionItemsResult>.Fail(MtgErrors.NotFound("會議"));

        var extras = new List<string>();
        foreach (var raw in request?.ExtraRecipients ?? [])
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            if (!ActionItemRules.IsValidEmail(raw, out var email)) return MtgResult<NotifyActionItemsResult>.Fail(MtgErrors.Validation($"額外收件人的信箱格式不正確：{Mask(raw)}"));
            if (!extras.Contains(email, StringComparer.OrdinalIgnoreCase)) extras.Add(email);
        }

        if (extras.Count > MaxExtraRecipients) return MtgResult<NotifyActionItemsResult>.Fail(MtgErrors.Validation($"額外收件人最多 {MaxExtraRecipients} 位。"));

        var ids = request?.ItemIds;
        var query = db.ActionItems.Where(a => a.MeetingId == meetingId);
        query = ids is { Count: > 0 } ? query.Where(a => ids.Contains(a.Id)) : query.Where(a => a.Status == ActionItemStatus.Open || a.Status == ActionItemStatus.InProgress);
        var items = await query.OrderBy(a => a.Seq).ToListAsync(ct).ConfigureAwait(false);
        if (items.Count == 0) return MtgResult<NotifyActionItemsResult>.Fail(MtgErrors.Validation("沒有可通知的待辦事項。"));

        var failures = new List<string>();
        var notified = new HashSet<Guid>();
        var ownerMails = 0;
        var summaryMails = 0;

        var byOwner = items.Where(a => ActionItemRules.IsValidEmail(a.OwnerEmail, out _))
            .GroupBy(a => a.OwnerEmail!.Trim(), StringComparer.OrdinalIgnoreCase).ToList();
        var skipped = items.Count(a => !ActionItemRules.IsValidEmail(a.OwnerEmail, out _));

        try
        {
            foreach (var group in byOwner)
            {
                var list = group.ToList();
                if (await TrySendAsync([group.Key], BuildMail(meeting.Title, list, showOwner: false), failures, ct).ConfigureAwait(false))
                {
                    ownerMails++;
                    foreach (var a in list) notified.Add(a.Id);
                }
            }

            if (extras.Count > 0 && await TrySendAsync(extras, BuildMail(meeting.Title, items, showOwner: true), failures, ct).ConfigureAwait(false))
            {
                summaryMails++;
                foreach (var a in items) notified.Add(a.Id);
            }
        }
        catch (MailNotConfiguredException)
        {
            return MtgResult<NotifyActionItemsResult>.Fail(MtgErrors.MailUnavailable());
        }

        var now = clock.GetUtcNow();
        foreach (var a in items.Where(a => notified.Contains(a.Id))) a.NotifiedAtUtc = now;
        if (notified.Count > 0) await db.SaveChangesAsync(ct).ConfigureAwait(false);

        // 只記錄數量，不記錄收件人信箱與待辦內容
        logger.LogInformation("[MTG] {Actor} 通知會議 {MeetingId} 的待辦：寄出 {Owner} 封負責人信、{Summary} 封清單信，失敗 {Failed}，略過（無信箱） {Skipped}。",
            actor, meetingId, ownerMails, summaryMails, failures.Count, skipped);

        return MtgResult<NotifyActionItemsResult>.Ok(new NotifyActionItemsResult(notified.Count, ownerMails, summaryMails, skipped, failures));
    }

    private async Task<bool> TrySendAsync(IReadOnlyList<string> to, MailContent content, List<string> failures, CancellationToken ct)
    {
        try
        {
            await mailSender!.SendAsync(new MailRequest(to, [], content.Subject, content.Html), ct).ConfigureAwait(false);
            return true;
        }
        catch (MailNotConfiguredException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[MTG] 待辦通知信寄送失敗。");
            failures.Add($"寄給 {string.Join("、", to.Select(Mask))} 失敗，請稍後重試。");
            return false;
        }
    }

    private sealed record MailContent(string Subject, string Html);

    private static MailContent BuildMail(string meetingTitle, IReadOnlyList<ActionItem> items, bool showOwner)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

        var sb = new StringBuilder();
        sb.Append("<p>會議「").Append(E(meetingTitle)).Append("」有 ").Append(items.Count).Append(" 項待辦事項：</p>");
        sb.Append("<table style=\"border-collapse:collapse;font-size:14px\"><thead><tr>");
        foreach (var h in showOwner ? new[] { "#", "待辦事項", "負責人", "期限", "備註" } : new[] { "#", "待辦事項", "期限", "備註" })
            sb.Append("<th style=\"border:1px solid #ccc;padding:6px 10px;background:#f3f3f3;text-align:left\">").Append(h).Append("</th>");
        sb.Append("</tr></thead><tbody>");

        var n = 0;
        foreach (var a in items)
        {
            n++;
            var due = a.DueDate is { } d ? d.ToString("yyyy-MM-dd") : a.DueText ?? "未訂";
            sb.Append("<tr>");
            Cell(sb, n.ToString());
            Cell(sb, E(a.Description));
            if (showOwner) Cell(sb, E(a.Owner ?? "未指派"));
            Cell(sb, E(due));
            Cell(sb, E(a.Note));
            sb.Append("</tr>");
        }

        sb.Append("</tbody></table><p style=\"color:#707070;font-size:12px\">此信由會議助理自動寄出，請依會議紀要與團隊約定追蹤。</p>");
        return new MailContent(ActionItemRules.SafeSubject($"[待辦通知] {meetingTitle}（{items.Count} 項）"), sb.ToString());

        static void Cell(StringBuilder b, string html) => b.Append("<td style=\"border:1px solid #ccc;padding:6px 10px\">").Append(html).Append("</td>");
    }

    /// <summary>遮蔽信箱（a***@domain），用於錯誤訊息與日誌。</summary>
    public static string Mask(string email)
    {
        var at = email.IndexOf('@');
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }

    private static ActionItemDto ToDto(ActionItem a) => new(a.Id, a.MeetingId, a.Seq, a.Description, a.Owner, a.OwnerEmail, a.DueDate, a.DueText,
        a.Status, a.Note, a.Source, a.MinutesVersion, a.NotifiedAtUtc, a.CreatedAtUtc, a.UpdatedAtUtc);
}
