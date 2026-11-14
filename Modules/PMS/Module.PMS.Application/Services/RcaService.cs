using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Abstractions.Notifications;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// P1 重大異常分析報告 RCA（FORM-RCA-01，SOP 7.3）：Tech Lead 撰寫技術根因並提交（系統恢復後 48 小時內），
/// PM 潤飾後寄送客戶；寄送後內容鎖定。每張問題單至多一份；專案資料列授權：非內部成員一律 404。
/// <para>
/// PDF：內部成員可隨時預覽／下載（未寄送前即時產生並標示「內部預覽」）。寄送有兩種方式：
/// <b>系統代寄</b>＝產生正式版 PDF 存入 FMS（<c>pms-form</c>，加密）並以 Email 附件寄給 PM 指定的客戶收件人；
/// <b>僅記錄</b>＝PM 已自行寄出，只記錄對象與方式。代寄先寄信、成功後才標記 <c>Sent</c>；寄信失敗則回收 FMS 檔案、狀態維持待寄送。
/// </para>
/// </summary>
public sealed class RcaService(
    PMSDbContext db,
    IFileManagementService files,
    PmsMailNotifier notifier,
    TimeProvider clock,
    ILogger<RcaService> logger,
    IMailSender? mailSender = null)
{
    public const string FileCategory = "pms-form";

    public const string ReferenceType = "PmsRca";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---------- 取得 ----------

    public async Task<PmsResult<RcaDto>> GetAsync(Guid userId, Guid ticketId, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, ticketId, track: false, ct).ConfigureAwait(false);
        return ctx.Error is { } error ? error : PmsResult<RcaDto>.Success(ToDto(ctx.Value!));
    }

    // ---------- 建立／更新草稿 ----------

    public async Task<PmsResult<RcaDto>> UpsertAsync(Guid userId, string actor, Guid ticketId, UpsertRcaRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadAsync(userId, ticketId, track: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;

        var isTl = ProjectAccessPolicy.IsTechLead(userId, c.Members);
        var isPm = ProjectAccessPolicy.IsPm(userId, c.Members);
        if (!isTl && !isPm) return PmsError.Forbidden("僅專案 Tech Lead 或 PM 可撰寫 RCA。");

        var invalid = RcaRules.ValidateDraft(
            request.Trigger, request.RecoveredAt, request.Summary, request.RootCause, request.ShortTermAction, request.LongTermPrevention, request.Timeline);
        if (invalid is not null) return PmsError.Validation(invalid);

        if (!c.Eligible.Contains(request.Trigger))
            return PmsError.Validation(c.Eligible.Count == 0
                ? "僅已分流的「系統異常」問題單可建立 RCA。"
                : $"此問題單不符合「{request.Trigger}」觸發條件。");

        var now = clock.GetUtcNow();
        var timeline = (request.Timeline ?? []).OrderBy(t => t.At).Select(t => new RcaTimelineItem(t.At, t.Event.Trim())).ToList();

        if (c.Rca is null)
        {
            var rca = new RcaReport
            {
                Id = Guid.NewGuid(), TicketId = c.Ticket.Id, ProjectId = c.Ticket.ProjectId, Status = RcaStatus.Draft,
                CreatedBy = actor, CreatedAtUtc = now
            };
            Apply(rca, request, timeline, actor, now);
            db.RcaReports.Add(rca);

            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                db.ChangeTracker.Clear();
                return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "此問題單的 RCA 剛被他人建立，請重新載入。");
            }

            db.ChangeTracker.Clear();
            return await GetAsync(userId, ticketId, ct).ConfigureAwait(false);
        }

        // 更新既有：Draft 時 TL／PM 皆可改；Submitted 時僅 PM 潤飾；Sent 鎖定
        var rcaStatus = c.Rca.Status;
        if (rcaStatus == RcaStatus.Sent) return PmsError.Conflict("PMS.CONTENT_LOCKED", "RCA 已寄送客戶，內容鎖定。");
        if (rcaStatus == RcaStatus.Submitted && !isPm) return PmsError.Forbidden("RCA 已提交，僅 PM 可潤飾。");

        if (!TryParseRowVersion(request.RowVersion, out var bytes)) return PmsError.Validation("更新 RCA 須帶 rowVersion。");
        db.Entry(c.Rca).Property(x => x.RowVersion).OriginalValue = bytes;

        Apply(c.Rca, request, timeline, actor, now);
        return await SaveAsync(userId, ticketId, ct).ConfigureAwait(false);
    }

    // ---------- 提交（Tech Lead） ----------

    public async Task<PmsResult<RcaDto>> SubmitAsync(Guid userId, string actor, Guid ticketId, SubmitRcaRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadForActionAsync(userId, ticketId, request.RowVersion, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;
        var rca = c.Rca!;

        if (!ProjectAccessPolicy.IsTechLead(userId, c.Members)) return PmsError.Forbidden("僅專案 Tech Lead 可提交 RCA。");
        if (rca.Status != RcaStatus.Draft) return PmsError.Conflict("PMS.INVALID_TRANSITION", "RCA 已提交。");

        var timeline = ReadTimeline(rca.TimelineJson);
        var incomplete = RcaRules.ValidateForSubmit(rca.Summary, rca.RootCause, rca.ShortTermAction, rca.LongTermPrevention, timeline);
        if (incomplete is not null) return PmsError.Validation(incomplete);

        var now = clock.GetUtcNow();
        rca.Status = RcaStatus.Submitted;
        rca.SubmittedBy = userId;
        rca.SubmittedAt = now;
        Touch(rca, actor, now);
        return await SaveAsync(userId, ticketId, ct).ConfigureAwait(false);
    }

    // ---------- 寄送客戶（PM） ----------

    public async Task<PmsResult<RcaDto>> SendAsync(Guid userId, string actor, Guid ticketId, SendRcaRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Note is { Length: > 500 }) return PmsError.Validation("寄送備註不得超過 500 字。");
        if (request.Message is { Length: > RcaMail.MaxMessageLength }) return PmsError.Validation($"補充說明不得超過 {RcaMail.MaxMessageLength} 字。");
        if (!Enum.IsDefined(request.Mode)) return PmsError.Validation("寄送方式不正確。");

        var ctx = await LoadForActionAsync(userId, ticketId, request.RowVersion, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;
        var rca = c.Rca!;

        if (!ProjectAccessPolicy.IsPm(userId, c.Members)) return PmsError.Forbidden("僅專案 PM 可寄送 RCA。");
        if (rca.Status != RcaStatus.Submitted) return PmsError.Conflict("PMS.INVALID_TRANSITION", "須待 Tech Lead 提交後，PM 才能寄送。");

        var now = clock.GetUtcNow();
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        if (request.Mode == RcaSendMode.RecordOnly)
        {
            rca.Status = RcaStatus.Sent;
            rca.SentBy = userId;
            rca.SentAt = now;
            rca.SentNote = note;
            Touch(rca, actor, now);
            return await SaveAsync(userId, ticketId, ct).ConfigureAwait(false);
        }

        return await EmailAndMarkSentAsync(c, rca, userId, actor, request, note, now, ct).ConfigureAwait(false);
    }

    /// <summary>系統代寄：產生正式版 PDF → 存 FMS → Email 附件寄出 → 標記已寄送。寄信失敗時回收 FMS 檔案，狀態維持待寄送。</summary>
    private async Task<PmsResult<RcaDto>> EmailAndMarkSentAsync(
        Context c, RcaReport rca, Guid userId, string actor, SendRcaRequest request, string? note, DateTimeOffset now, CancellationToken ct)
    {
        if (mailSender is null)
            return new PmsError("PMS.MAIL_UNAVAILABLE", 503, "Service Unavailable", "系統未啟用郵件寄送功能，請改用「僅記錄」並自行寄出，或聯絡管理員檢查 SMTP 設定。");

        var (to, toError) = RcaMail.ParseRecipients(request.To, "收件人", required: true);
        if (toError is not null) return PmsError.Validation(toError);
        var (cc, ccError) = RcaMail.ParseRecipients(request.Cc, "副本", required: false);
        if (ccError is not null) return PmsError.Validation(ccError);
        if (to.Count + cc.Count > RcaMail.MaxRecipients * 2) return PmsError.Validation("收件人與副本合計過多。");

        var pdf = RcaReportPdf.Render(ToPdfData(c, rca, official: true, now));
        var fileName = RcaMail.FileName(DateOnly.FromDateTime(now.ToOffset(SlaCalculator.Offset).DateTime), c.Ticket.TicketNo);

        FileMetadataDto stored;
        try
        {
            using var stream = new MemoryStream(pdf, writable: false);
            stored = await files.UploadAsync(
                new FileUploadRequest(stream, fileName, FileCategory, ReferenceType, rca.Id.ToString(), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pdf)).ToLowerInvariant()),
                FileAccessScope.Internal, ct).ConfigureAwait(false);
        }
        catch (FileRejectedException ex)
        {
            return MapRejected(ex);
        }

        // 回覆地址設為寄件 PM，客戶回信會回到 PM 而不是系統信箱
        var replyTo = (await notifier.ResolveEmailsAsync([userId], ct).ConfigureAwait(false)).FirstOrDefault();

        try
        {
            await mailSender.SendAsync(new MailRequest(
                to, cc, RcaMail.Subject(c.Project.Code, c.Ticket.TicketNo), RcaMail.Body(c.Project.Name, c.Ticket.TicketNo, request.Message),
                [new MailAttachment(fileName, "application/pdf", pdf)], replyTo), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await CompensateAsync(stored.Id).ConfigureAwait(false);
            logger.LogError(ex, "[PMS] RCA 代寄失敗：{TicketNo}（{ExceptionType}）", c.Ticket.TicketNo, ex.GetType().Name);

            return ex is MailNotConfiguredException
                ? new PmsError("PMS.MAIL_UNAVAILABLE", 503, "Service Unavailable", "系統尚未設定 SMTP，無法代寄；請改用「僅記錄」並自行寄出，或聯絡管理員。")
                : new PmsError("PMS.MAIL_SEND_FAILED", 502, "Bad Gateway", "郵件伺服器拒絕或無法連線，RCA 尚未寄出（狀態維持待寄送），請稍後重試或改用「僅記錄」。");
        }

        // 信已寄出：標記已寄送並記錄對象與存檔 PDF；此後若寫入失敗，信件已在客戶手上，須明確提示人工處理
        rca.Status = RcaStatus.Sent;
        rca.SentBy = userId;
        rca.SentAt = now;
        rca.SentNote = note;
        rca.SentTo = RcaMail.FormatSentTo(to, cc);
        rca.SentPdfFileId = stored.Id;
        rca.SentPdfFileName = fileName;
        Touch(rca, actor, now);

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is DbUpdateException or DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            logger.LogError(ex, "[PMS] RCA {TicketNo} 郵件已寄出，但記錄寄送狀態失敗，請人工確認。", c.Ticket.TicketNo);
            return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "郵件已寄出，但記錄寄送狀態失敗（資料剛被他人更新）。請重新整理確認狀態，勿重複寄送。");
        }

        db.ChangeTracker.Clear();
        return await GetAsync(userId, c.Ticket.Id, ct).ConfigureAwait(false);
    }

    // ---------- PDF 預覽／下載 ----------

    /// <summary>
    /// 取得 RCA 的 PDF：已寄送者回傳當時存檔的正式版；未寄送者即時產生（標示「內部預覽」）。專案內部成員可用。
    /// </summary>
    public async Task<PmsResult<RcaPdfFile>> GetPdfAsync(Guid userId, Guid ticketId, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, ticketId, track: false, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!;
        if (c.Rca is null) return PmsError.NotFound("此問題單尚未建立 RCA。");

        if (c.Rca is { Status: RcaStatus.Sent, SentPdfFileId: { } fileId })
        {
            try
            {
                var download = await files.OpenDownloadAsync(fileId, FileAccessScope.Internal, ct).ConfigureAwait(false);
                if (download?.Content is not null)
                {
                    await using var content = download.Content;
                    using var buffer = new MemoryStream();
                    await content.CopyToAsync(buffer, ct).ConfigureAwait(false);
                    return PmsResult<RcaPdfFile>.Success(new RcaPdfFile(buffer.ToArray(), c.Rca.SentPdfFileName ?? $"{c.Ticket.TicketNo}_RCA.pdf"));
                }
            }
            catch (FileRejectedException ex)
            {
                logger.LogWarning(ex, "[PMS] RCA {TicketNo} 的存檔 PDF 無法讀取，改以即時產生。", c.Ticket.TicketNo);
            }
        }

        var official = c.Rca.Status == RcaStatus.Sent;
        var pdf = RcaReportPdf.Render(ToPdfData(c, c.Rca, official, c.Now));
        return PmsResult<RcaPdfFile>.Success(new RcaPdfFile(pdf, $"{c.Ticket.TicketNo}_RCA{(official ? string.Empty : "_內部預覽")}.pdf"));
    }

    // ---------- 內部輔助 ----------

    private sealed record Context(
        Ticket Ticket, Project Project, IReadOnlyList<(Guid UserId, RaciRole Role)> Members, RcaReport? Rca,
        IReadOnlyList<RcaTrigger> Eligible, Guid UserId, DateTimeOffset Now, bool MailEnabled);

    private async Task<PmsResult<Context>> LoadAsync(Guid userId, Guid ticketId, bool track, CancellationToken ct)
    {
        var ticketQuery = track ? db.Tickets.AsQueryable() : db.Tickets.AsNoTracking();
        var ticket = await ticketQuery.FirstOrDefaultAsync(t => t.Id == ticketId, ct).ConfigureAwait(false);
        if (ticket is null) return PmsError.NotFound("問題單不存在。");

        var project = await db.Projects.AsNoTracking().Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Id == ticket.ProjectId, ct).ConfigureAwait(false);
        if (project is null) return PmsError.NotFound("問題單不存在。");

        var members = project.Members.Select(m => (m.UserId, m.RaciRole)).ToList();
        if (ProjectAccessPolicy.Evaluate(userId, members) != ProjectAccessLevel.Internal) return PmsError.NotFound("問題單不存在。");

        var rcaQuery = track ? db.RcaReports.AsQueryable() : db.RcaReports.AsNoTracking();
        var rca = await rcaQuery.FirstOrDefaultAsync(r => r.TicketId == ticketId, ct).ConfigureAwait(false);

        var now = clock.GetUtcNow();
        var sla = ticket.Status == TicketStatus.Cancelled ? SlaStatus.NotApplicable : SlaCalculator.Status(ticket.DueAt, ticket.ResolvedAt, now);
        var eligible = RcaRules.EligibleTriggers(ticket.Type, ticket.Status, ticket.Priority, sla);

        return PmsResult<Context>.Success(new Context(ticket, project, members, rca, eligible, userId, now, mailSender is not null));
    }

    private async Task<PmsResult<Context>> LoadForActionAsync(Guid userId, Guid ticketId, string? rowVersion, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, ticketId, track: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        if (ctx.Value!.Rca is null) return PmsError.NotFound("此問題單尚未建立 RCA。");

        if (!TryParseRowVersion(rowVersion, out var bytes)) return PmsError.Validation("rowVersion 格式不正確。");
        db.Entry(ctx.Value.Rca).Property(x => x.RowVersion).OriginalValue = bytes;
        return ctx;
    }

    private async Task<PmsResult<RcaDto>> SaveAsync(Guid userId, Guid ticketId, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "RCA 已被他人更新，請重新載入後再試。");
        }

        db.ChangeTracker.Clear();
        return await GetAsync(userId, ticketId, ct).ConfigureAwait(false);
    }

    private async Task CompensateAsync(Guid fileId)
    {
        try
        {
            await files.DeleteAsync(fileId, FileAccessScope.Internal, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PMS] RCA 寄信失敗，且回收 FMS 檔案 {FileId} 失敗，請維運清理孤兒檔案。", fileId);
        }
    }

    private static void Apply(RcaReport rca, UpsertRcaRequest request, List<RcaTimelineItem> timeline, string actor, DateTimeOffset now)
    {
        rca.Trigger = request.Trigger;
        rca.RecoveredAt = request.RecoveredAt;
        rca.DueAt = RcaRules.Due(request.RecoveredAt);
        rca.Summary = request.Summary?.Trim() ?? string.Empty;
        rca.TimelineJson = JsonSerializer.Serialize(timeline, Json);
        rca.RootCause = request.RootCause?.Trim() ?? string.Empty;
        rca.ShortTermAction = request.ShortTermAction?.Trim() ?? string.Empty;
        rca.LongTermPrevention = request.LongTermPrevention?.Trim() ?? string.Empty;
        Touch(rca, actor, now);
    }

    private static RcaPdfData ToPdfData(Context c, RcaReport rca, bool official, DateTimeOffset now) => new(
        c.Project.Code, c.Project.Name, c.Ticket.TicketNo, c.Ticket.Title, rca.Trigger, rca.RecoveredAt, rca.DueAt,
        rca.Summary, ReadTimeline(rca.TimelineJson), rca.RootCause, rca.ShortTermAction, rca.LongTermPrevention,
        rca.Status switch
        {
            RcaStatus.Draft => "撰寫中",
            RcaStatus.Submitted => "已提交，待 PM 寄送",
            _ => "已寄送"
        },
        now, official);

    private static RcaDto ToDto(Context c)
    {
        var rca = c.Rca;
        var isTl = ProjectAccessPolicy.IsTechLead(c.UserId, c.Members);
        var isPm = ProjectAccessPolicy.IsPm(c.UserId, c.Members);
        var eligible = c.Eligible;

        if (rca is null)
        {
            var canCreate = (isTl || isPm) && eligible.Count > 0;
            return new RcaDto(
                false, null, c.Ticket.Id, c.Ticket.TicketNo, c.Ticket.Title, c.Project.Id, c.Project.Code, c.Project.Name, eligible,
                null, null, string.Empty, c.Ticket.ResolvedAt, null, false, [], string.Empty, string.Empty, string.Empty, [],
                null, null, null, null, null, null, false, c.MailEnabled, new RcaActionsDto(canCreate, false, false), null);
        }

        var timeline = ReadTimeline(rca.TimelineJson);
        var canEdit = rca.Status switch
        {
            RcaStatus.Draft => isTl || isPm,
            RcaStatus.Submitted => isPm,
            _ => false
        };

        return new RcaDto(
            true, rca.Id, c.Ticket.Id, c.Ticket.TicketNo, c.Ticket.Title, c.Project.Id, c.Project.Code, c.Project.Name, eligible,
            rca.Trigger, rca.Status, rca.Summary, rca.RecoveredAt, rca.DueAt,
            RcaRules.IsOverdue(rca.Status, rca.DueAt, rca.SubmittedAt, c.Now), timeline,
            rca.RootCause, rca.ShortTermAction, rca.LongTermPrevention,
            RcaRules.NameWarnings(rca.Summary, rca.RootCause, rca.ShortTermAction, rca.LongTermPrevention, string.Join('\n', timeline.Select(t => t.Event))),
            rca.SubmittedBy, rca.SubmittedAt, rca.SentBy, rca.SentAt, rca.SentNote, rca.SentTo, rca.SentPdfFileId is not null, c.MailEnabled,
            new RcaActionsDto(canEdit, CanSubmit: rca.Status == RcaStatus.Draft && isTl, CanSend: rca.Status == RcaStatus.Submitted && isPm),
            Convert.ToBase64String(rca.RowVersion));
    }

    private static List<RcaTimelineItem> ReadTimeline(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<RcaTimelineItem>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void Touch(RcaReport r, string actor, DateTimeOffset now)
    {
        r.UpdatedBy = actor;
        r.UpdatedAtUtc = now;
    }

    private static bool TryParseRowVersion(string? value, out byte[] bytes)
    {
        bytes = [];
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { bytes = Convert.FromBase64String(value); return bytes.Length > 0; }
        catch (FormatException) { return false; }
    }

    private static PmsError MapRejected(FileRejectedException ex) => ex.StatusCode switch
    {
        503 => new PmsError("PMS.FILE_SERVICE_UNAVAILABLE", 503, "Service Unavailable", ex.Message),
        413 => new PmsError("PMS.FILE_REJECTED", 413, "Payload Too Large", ex.Message),
        415 => new PmsError("PMS.FILE_REJECTED", 415, "Unsupported Media Type", ex.Message),
        403 => new PmsError("PMS.FILE_REJECTED", 403, "Forbidden", ex.Message),
        _ => new PmsError("PMS.FILE_REJECTED", 400, "Invalid Request", ex.Message)
    };
}
