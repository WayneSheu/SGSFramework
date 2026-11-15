using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Abstractions.Notifications;
using SGSFramework.Core.FileManagement;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// 專案合約履約管理（乙方標準版第二期）：甲方協力事項與遲延通知書（3 個工作日內書面通知、工期自動順延）、文件審查回合（7 個工作日／2 次、
/// 逾期擬制通過）、驗收與擬制驗收（14 個工作日）、合約變更（CR 核准自動掛帳＋手動）與付款期程。
/// 僅合約成員可見（非成員 404）；業務與合約 PM 可維護。日期以台灣時間判定，工作日以國定假日曆計算。
/// 展延通知書的 Email 代寄沿用 RCA 的模式：先存正式 PDF 到 FMS、寄信成功才登記，失敗則回收檔案。
/// </summary>
public sealed class ContractFulfillmentService(
    PMSDbContext db,
    HolidayService holidays,
    IFileManagementService files,
    PmsMailNotifier notifier,
    TimeProvider clock,
    IConfiguration configuration,
    ILogger<ContractFulfillmentService> logger,
    IMailSender? mailSender = null)
{
    public const string NoticeFileCategory = "pms-form";
    public const string NoticeReferenceType = "PmsContractNotice";

    private const int MaxRounds = 30;

    // ---------- 查詢 ----------

    public async Task<PmsResult<FulfillmentDto>> GetAsync(Guid userId, Guid contractId, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, contractId, track: false, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!.Contract;
        var members = ctx.Value.Members;

        var today = Today();
        var calendar = await CalendarAsync(today, ct).ConfigureAwait(false);
        var manage = ContractRules.CanManage(userId, members);

        var coopItems = await db.ContractCoopItems.AsNoTracking().Include(i => i.Notices)
            .Where(i => i.ContractId == contractId && !i.IsDeleted).OrderBy(i => i.DueDate).ThenBy(i => i.CreatedAtUtc).ToListAsync(ct).ConfigureAwait(false);
        var notices = await db.ContractExtensionNotices.AsNoTracking().Where(n => n.ContractId == contractId).OrderByDescending(n => n.Number).ToListAsync(ct).ConfigureAwait(false);
        var rounds = await db.ContractReviewRounds.AsNoTracking().Where(r => r.ContractId == contractId).ToListAsync(ct).ConfigureAwait(false);
        var acceptance = await db.ContractAcceptances.AsNoTracking().FirstOrDefaultAsync(a => a.ContractId == contractId, ct).ConfigureAwait(false);
        var amendments = await db.ContractAmendments.AsNoTracking().Where(a => a.ContractId == contractId).OrderByDescending(a => a.EffectiveDate).ThenByDescending(a => a.CreatedAtUtc).ToListAsync(ct).ConfigureAwait(false);
        var payments = await db.ContractPaymentTerms.AsNoTracking().Where(p => p.ContractId == contractId).OrderBy(p => p.SortOrder).ToListAsync(ct).ConfigureAwait(false);

        var coopDtos = coopItems.Select(i =>
        {
            var latestExt = i.Notices.Count == 0 ? 0 : i.Notices.Max(n => n.ExtensionWorkdays);
            var delay = ContractFulfillmentRules.DelayWorkdays(i.DueDate, i.CompletedDate, today, calendar);
            var noticeDays = ContractFulfillmentRules.EffectiveNoticeWorkdays(i.NoticeWorkdays, c.CoopNoticeWorkdays);
            return new CoopItemDto(
                i.Id, i.Title, i.Kind, i.Description, i.DueDate, i.CompletedDate, i.Status,
                ContractFulfillmentRules.State(i, i.Notices.Count > 0, today, calendar, noticeDays), delay, ContractFulfillmentRules.NoticeDeadline(i.DueDate, calendar, noticeDays),
                i.Notices.Count, latestExt, ContractFulfillmentRules.NeedsNotice(i, delay, latestExt), Convert.ToBase64String(i.RowVersion), noticeDays, i.NoticeWorkdays);
        }).ToList();

        var coopTitles = coopItems.ToDictionary(i => i.Id, i => i.Title);
        var noticeDtos = notices.Select(n => new ExtensionNoticeDto(
            n.Id, n.Number, n.CoopItemId, coopTitles.GetValueOrDefault(n.CoopItemId, "（已刪除的協力事項）"), n.DelayWorkdays, n.ExtensionWorkdays,
            n.RequestedBy, n.Mode, n.SentTo, n.Note, n.PdfFileId is not null, n.SentByName, n.SentAt, n.Subject, n.BodyText, n.Closing, n.Signature)).ToList();

        var signoffItems = c.DeliverableItems.Where(i => i.RequiresCustomerSignoff).OrderBy(i => i.SortOrder).ToList();
        var itemNames = c.DeliverableItems.ToDictionary(i => i.Id, i => i.Name);
        var active = IsPerformance(c.Status);

        var tracks = signoffItems.Select(i =>
        {
            var mine = rounds.Where(r => r.DeliverableItemId == i.Id).ToList();
            var last = mine.OrderByDescending(r => r.RoundNo).FirstOrDefault();
            return new ReviewTrackDto(i.Id, i.Name, i.Stage, mine.Count, c.MaxReviewRounds, last?.Outcome,
                manage && active && ContractFulfillmentRules.ValidateNewRound(mine, true) is null);
        }).ToList();

        var roundDtos = rounds.OrderByDescending(r => r.SubmittedDate).ThenByDescending(r => r.RoundNo).Select(r => new ReviewRoundDto(
            r.Id, r.DeliverableItemId, itemNames.GetValueOrDefault(r.DeliverableItemId, "（已移除的交付項目）"), r.RoundNo, r.SubmittedDate, r.ResponseDue,
            r.Outcome, r.RespondedDate, r.Note, r.ExceedsRoundLimit, r.Outcome == ReviewOutcome.Pending && r.ResponseDue is { } due && today > due)).ToList();

        var accStatus = acceptance?.Status ?? AcceptanceStatus.NotDelivered;
        int? daysLeft = null;
        if (accStatus == AcceptanceStatus.Delivered && acceptance?.DeemedDue is { } deemed)
            daysLeft = today <= deemed ? ContractFulfillmentRules.WorkdaysBetween(today, deemed, calendar) : -ContractFulfillmentRules.WorkdaysBetween(deemed, today, calendar);

        var acceptanceDto = new AcceptanceDto(
            accStatus, acceptance?.DeliveredDate, acceptance?.DeemedDue, acceptance?.DefectReportedDate, acceptance?.DefectNote, acceptance?.AcceptedDate,
            acceptance?.Note, daysLeft, acceptance is null ? string.Empty : Convert.ToBase64String(acceptance.RowVersion));

        var amendmentDtos = amendments.Select(a => new AmendmentDto(
            a.Id, a.Source, a.CrNo, a.Title, a.AmountDelta, a.NewDeliveryDate, a.WarrantyMonthsDelta, a.EffectiveDate, a.Note, a.CreatedBy, a.CreatedAtUtc)).ToList();

        var current = ContractFulfillmentRules.CurrentAmount(c.TotalAmount, amendments);
        var accepted = ContractFulfillmentRules.IsAccepted(accStatus);
        var amounts = ContractFulfillmentRules.Amounts(current, payments.Select(p => p.Percent).ToList());
        var paymentDtos = payments.Select((p, idx) => new PaymentTermDto(
            p.Id, p.SortOrder, p.Name, p.Percent, amounts[idx], p.Trigger, p.PlannedDate, p.Status, p.InvoiceNo, p.InvoicedDate, p.PaidDate, p.InvoicedAmount,
            manage && p.Status == PaymentStatus.Pending && ContractFulfillmentRules.ValidateMove(p, PaymentStatus.Invoiceable, c.Status, accepted, null, null, today) is null,
            manage && p.Status == PaymentStatus.Invoiceable,
            manage && p.Status == PaymentStatus.Invoiced)).ToList();

        var extension = ContractFulfillmentRules.TotalExtensionWorkdays(notices.Select(n => (n.CoopItemId, n.ExtensionWorkdays)));
        var currentWarranty = ContractFulfillmentRules.CurrentWarrantyMonths(c.WarrantyMonths, amendments);
        var summary = new FulfillmentSummaryDto(
            c.TotalAmount, amendments.Sum(a => a.AmountDelta), current,
            payments.Where(p => p.Status is PaymentStatus.Invoiced or PaymentStatus.Paid).Sum(p => p.InvoicedAmount ?? 0),
            payments.Where(p => p.Status == PaymentStatus.Paid).Sum(p => p.InvoicedAmount ?? 0),
            c.DeliveryDate, ContractFulfillmentRules.AmendedDeliveryDate(c.DeliveryDate, amendments), extension,
            ContractFulfillmentRules.EffectiveDeliveryDate(c.DeliveryDate, amendments, extension, calendar),
            c.WarrantyMonths, currentWarranty, acceptance?.AcceptedDate is { } acceptedDate ? acceptedDate.AddMonths(currentWarranty) : null);

        var actions = new FulfillmentActionsDto(
            manage,
            manage && ContractFulfillmentRules.ValidateDeliver(c.Status, accStatus, today, today) is null,
            manage && active && accStatus == AcceptanceStatus.Delivered,
            manage && active && ContractFulfillmentRules.ValidateConfirm(accStatus, acceptance?.DeliveredDate, today, today) is null,
            manage && IsSigned(c.Status) && payments.All(p => p.Status is PaymentStatus.Pending or PaymentStatus.Invoiceable),
            active);

        return PmsResult<FulfillmentDto>.Success(new FulfillmentDto(summary, coopDtos, noticeDtos, tracks, roundDtos, acceptanceDto, amendmentDtos, paymentDtos, actions));
    }

    // ---------- 甲方協力事項 ----------

    public async Task<PmsResult<FulfillmentDto>> CreateCoopItemAsync(Guid userId, string actor, Guid contractId, CoopItemRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, contractId, requirePerformance: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!.Contract;

        var invalid = ContractFulfillmentRules.ValidateCoopItem(request.Title, request.Description, request.DueDate, request.NoticeWorkdays);
        if (invalid is not null) return PmsError.Validation(invalid);
        if (!Enum.IsDefined(request.Kind)) return PmsError.Validation("協力事項類型不正確。");
        if (await db.ContractCoopItems.CountAsync(i => i.ContractId == contractId && !i.IsDeleted, ct).ConfigureAwait(false) >= ContractFulfillmentRules.MaxCoopItems)
            return PmsError.Validation($"每份合約最多 {ContractFulfillmentRules.MaxCoopItems} 個協力事項。");

        var now = clock.GetUtcNow();
        var item = new ContractCoopItem
        {
            Id = Guid.NewGuid(), ContractId = contractId, Title = request.Title.Trim(), Kind = request.Kind,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(), DueDate = request.DueDate,
            NoticeWorkdays = request.NoticeWorkdays, CreatedBy = actor, CreatedAtUtc = now
        };
        db.ContractCoopItems.Add(item);
        AddEvent(c, ContractEventType.CoopItemAdded, $"新增甲方協力事項「{item.Title}」（甲方應完成日 {item.DueDate:yyyy-MM-dd}）", userId, actor, now);
        return await SaveAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<FulfillmentDto>> UpdateCoopItemAsync(Guid userId, string actor, Guid contractId, Guid itemId, CoopItemRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, contractId, requirePerformance: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;

        var invalid = ContractFulfillmentRules.ValidateCoopItem(request.Title, request.Description, request.DueDate, request.NoticeWorkdays);
        if (invalid is not null) return PmsError.Validation(invalid);
        if (!Enum.IsDefined(request.Kind)) return PmsError.Validation("協力事項類型不正確。");

        var item = await db.ContractCoopItems.Include(i => i.Notices).FirstOrDefaultAsync(i => i.Id == itemId && i.ContractId == contractId && !i.IsDeleted, ct).ConfigureAwait(false);
        if (item is null) return PmsError.NotFound("協力事項不存在。");
        if (item.Notices.Count > 0 && (item.DueDate != request.DueDate || item.NoticeWorkdays != request.NoticeWorkdays))
            return PmsError.Conflict("PMS.CONTENT_LOCKED", "已寄出遲延通知書，甲方應完成日與通知期限不可更改（通知書是書面證據）。");
        if (!TryParseRowVersion(request.RowVersion, out var bytes)) return PmsError.Validation("rowVersion 格式不正確。");
        db.Entry(item).Property(x => x.RowVersion).OriginalValue = bytes;

        var now = clock.GetUtcNow();
        item.Title = request.Title.Trim();
        item.Kind = request.Kind;
        item.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        item.DueDate = request.DueDate;
        item.NoticeWorkdays = request.NoticeWorkdays;
        item.UpdatedBy = actor;
        item.UpdatedAtUtc = now;
        return await SaveAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<FulfillmentDto>> CompleteCoopItemAsync(Guid userId, string actor, Guid contractId, Guid itemId, CompleteCoopRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, contractId, requirePerformance: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!.Contract;

        var item = await db.ContractCoopItems.FirstOrDefaultAsync(i => i.Id == itemId && i.ContractId == contractId && !i.IsDeleted, ct).ConfigureAwait(false);
        if (item is null) return PmsError.NotFound("協力事項不存在。");
        if (item.Status != CoopStatus.Pending) return PmsError.Conflict("PMS.INVALID_TRANSITION", "這項協力事項已結束。");
        if (request.CompletedDate > Today()) return PmsError.Validation("完成日不得晚於今天。");
        if (!TryParseRowVersion(request.RowVersion, out var bytes)) return PmsError.Validation("rowVersion 格式不正確。");
        db.Entry(item).Property(x => x.RowVersion).OriginalValue = bytes;

        var now = clock.GetUtcNow();
        item.Status = CoopStatus.Completed;
        item.CompletedDate = request.CompletedDate;
        item.UpdatedBy = actor;
        item.UpdatedAtUtc = now;
        AddEvent(c, ContractEventType.CoopItemAdded, $"甲方協力事項「{item.Title}」已完成（{request.CompletedDate:yyyy-MM-dd}）", userId, actor, now);
        return await SaveAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    /// <summary>免除協力事項（甲方不需提供或乙方決定不需要）；已寄出通知書者不可免除。</summary>
    public async Task<PmsResult<FulfillmentDto>> WaiveCoopItemAsync(Guid userId, string actor, Guid contractId, Guid itemId, CancellationToken ct)
    {
        var ctx = await LoadManageAsync(userId, contractId, requirePerformance: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!.Contract;

        var item = await db.ContractCoopItems.Include(i => i.Notices).FirstOrDefaultAsync(i => i.Id == itemId && i.ContractId == contractId && !i.IsDeleted, ct).ConfigureAwait(false);
        if (item is null) return PmsError.NotFound("協力事項不存在。");
        if (item.Status != CoopStatus.Pending) return PmsError.Conflict("PMS.INVALID_TRANSITION", "這項協力事項已結束。");
        if (item.Notices.Count > 0) return PmsError.Conflict("PMS.CONTENT_LOCKED", "已寄出遲延通知書的協力事項不可免除；請改為記錄甲方完成。");

        var now = clock.GetUtcNow();
        item.Status = CoopStatus.Waived;
        item.UpdatedBy = actor;
        item.UpdatedAtUtc = now;
        AddEvent(c, ContractEventType.CoopItemAdded, $"免除甲方協力事項「{item.Title}」", userId, actor, now);
        return await SaveAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    /// <summary>刪除（軟刪除）：僅限尚未寄出通知書的協力事項。</summary>
    public async Task<PmsResult<FulfillmentDto>> DeleteCoopItemAsync(Guid userId, string actor, Guid contractId, Guid itemId, CancellationToken ct)
    {
        var ctx = await LoadManageAsync(userId, contractId, requirePerformance: false, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;

        var item = await db.ContractCoopItems.Include(i => i.Notices).FirstOrDefaultAsync(i => i.Id == itemId && i.ContractId == contractId && !i.IsDeleted, ct).ConfigureAwait(false);
        if (item is null) return PmsError.NotFound("協力事項不存在。");
        if (item.Notices.Count > 0) return PmsError.Conflict("PMS.CONTENT_LOCKED", "已寄出遲延通知書的協力事項不可刪除（保留書面證據）。");

        item.IsDeleted = true;
        item.UpdatedBy = actor;
        item.UpdatedAtUtc = clock.GetUtcNow();
        return await SaveAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    // ---------- 遲延通知書 ----------

    /// <summary>預覽通知書 PDF（未寄出，標示「預覽」）；順延工作日與請甲方提供的期限可帶預設。</summary>
    public async Task<PmsResult<NoticePdfFile>> PreviewNoticeAsync(Guid userId, string actor, Guid contractId, Guid itemId, NoticePreviewRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadAsync(userId, contractId, track: false, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;

        var prepared = await PrepareNoticeAsync(ctx.Value!.Contract, itemId, request.ExtensionWorkdays, request.RequestedBy, ct).ConfigureAwait(false);
        if (prepared.Error is { } preparedError) return preparedError;
        var p = prepared.Value!;

        var textError = ValidateOptionalText(request.Subject, request.BodyText) ?? ContractFulfillmentRules.ValidateNoticeSignoff(request.Closing, request.Signature);
        if (textError is not null) return PmsError.Validation(textError);

        var data = ToData(ctx.Value.Contract, p, actor, message: null, official: false, clock.GetUtcNow(), request.Subject, request.BodyText, request.Closing, request.Signature);
        return PmsResult<NoticePdfFile>.Success(new NoticePdfFile(ContractExtensionNoticePdf.Render(data), ContractExtensionNoticePdf.FileName(p.Today, ctx.Value.Contract.ContractNo, p.NextNumber)));
    }

    public async Task<PmsResult<FulfillmentDto>> SendNoticeAsync(Guid userId, string actor, Guid contractId, Guid itemId, SendNoticeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.IsDefined(request.Mode)) return PmsError.Validation("寄送方式不正確。");

        var ctx = await LoadManageAsync(userId, contractId, requirePerformance: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!.Contract;

        var prepared = await PrepareNoticeAsync(c, itemId, request.ExtensionWorkdays, request.RequestedBy, ct).ConfigureAwait(false);
        if (prepared.Error is { } preparedError) return preparedError;
        var p = prepared.Value!;

        var invalid = ContractFulfillmentRules.ValidateNotice(p.Delay, p.Extension, p.RequestedBy, p.Today, request.Note, request.Message)
                      ?? ValidateOptionalText(request.Subject, request.BodyText)
                      ?? ContractFulfillmentRules.ValidateNoticeSignoff(request.Closing, request.Signature);
        if (invalid is not null) return PmsError.Validation(invalid);

        // 寄出的最終文字（含未編輯時的預設文字）一併存檔，供日後稽核回溯
        var baseData = ToData(c, p, actor, request.Message, official: true, clock.GetUtcNow(), request.Subject, request.BodyText, request.Closing, request.Signature);
        var finalSubject = ContractExtensionNoticePdf.EffectiveSubject(baseData);
        var finalBody = ContractExtensionNoticePdf.EffectiveBody(baseData);
        var finalClosing = ContractExtensionNoticePdf.EffectiveClosing(baseData);
        var finalSignature = ContractExtensionNoticePdf.EffectiveSignature(baseData);

        var now = clock.GetUtcNow();
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        var notice = new ContractExtensionNotice
        {
            Id = Guid.NewGuid(), ContractId = contractId, CoopItemId = itemId, Number = p.NextNumber, DelayWorkdays = p.Delay, ExtensionWorkdays = p.Extension,
            RequestedBy = p.RequestedBy, Mode = request.Mode, Note = note, Subject = finalSubject, BodyText = finalBody, Closing = finalClosing, Signature = finalSignature, SentBy = userId, SentByName = actor, SentAt = now
        };

        if (request.Mode == NoticeMode.RecordOnly)
        {
            db.ContractExtensionNotices.Add(notice);
            AddEvent(c, ContractEventType.NoticeSent, $"記錄已寄出遲延通知書 #{notice.Number}（{p.Item.Title}，順延 {p.Extension} 個工作日；僅記錄）", userId, actor, now);
            return await SaveAsync(userId, contractId, ct).ConfigureAwait(false);
        }

        if (mailSender is null)
            return new PmsError("PMS.MAIL_UNAVAILABLE", 503, "Service Unavailable", "系統未啟用郵件寄送功能，請改用「僅記錄」並自行寄出，或聯絡管理員檢查 SMTP 設定。");

        var (to, toError) = RcaMail.ParseRecipients(request.To, "收件人", required: true);
        if (toError is not null) return PmsError.Validation(toError);
        var (cc, ccError) = RcaMail.ParseRecipients(request.Cc, "副本", required: false);
        if (ccError is not null) return PmsError.Validation(ccError);

        var data = ToData(c, p, actor, request.Message, official: true, now, finalSubject, finalBody, finalClosing, finalSignature);
        var pdf = ContractExtensionNoticePdf.Render(data);
        var fileName = ContractExtensionNoticePdf.FileName(p.Today, c.ContractNo, notice.Number);

        FileMetadataDto stored;
        try
        {
            using var stream = new MemoryStream(pdf, writable: false);
            stored = await files.UploadAsync(
                new FileUploadRequest(stream, fileName, NoticeFileCategory, NoticeReferenceType, notice.Id.ToString(), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pdf)).ToLowerInvariant()),
                FileAccessScope.Internal, ct).ConfigureAwait(false);
        }
        catch (FileRejectedException ex)
        {
            return MapRejected(ex);
        }

        var replyTo = (await notifier.ResolveEmailsAsync([userId], ct).ConfigureAwait(false)).FirstOrDefault();

        try
        {
            await mailSender.SendAsync(new MailRequest(
                to, cc, finalSubject, ContractExtensionNoticePdf.Body(data),
                [new MailAttachment(fileName, "application/pdf", pdf)], replyTo), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await CompensateAsync(stored.Id).ConfigureAwait(false);
            logger.LogError(ex, "[PMS] 合約遲延通知書代寄失敗：{ContractNo}（{ExceptionType}）", c.ContractNo, ex.GetType().Name);

            return ex is MailNotConfiguredException
                ? new PmsError("PMS.MAIL_UNAVAILABLE", 503, "Service Unavailable", "系統尚未設定 SMTP，無法代寄；請改用「僅記錄」並自行寄出，或聯絡管理員。")
                : new PmsError("PMS.MAIL_SEND_FAILED", 502, "Bad Gateway", "郵件伺服器拒絕或無法連線，通知書尚未寄出（未登記展延），請稍後重試或改用「僅記錄」。");
        }

        notice.SentTo = RcaMail.FormatSentTo(to, cc);
        notice.PdfFileId = stored.Id;
        notice.PdfFileName = fileName;
        db.ContractExtensionNotices.Add(notice);
        AddEvent(c, ContractEventType.NoticeSent, $"寄出遲延通知書 #{notice.Number}（{p.Item.Title}，順延 {p.Extension} 個工作日）給 {notice.SentTo}", userId, actor, now);

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is DbUpdateException or DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            logger.LogError(ex, "[PMS] 合約 {ContractNo} 遲延通知書已寄出，但登記失敗，請人工確認。", c.ContractNo);
            return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "通知書已寄出，但登記展延失敗（資料剛被他人更新）。請重新整理確認狀態，勿重複寄送。");
        }

        db.ChangeTracker.Clear();
        return await GetAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    /// <summary>下載已寄出的通知書 PDF（FMS 存檔的正式版，寫入存取稽核）。</summary>
    public async Task<PmsResult<NoticePdfFile>> GetNoticePdfAsync(Guid userId, Guid contractId, Guid noticeId, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, contractId, track: false, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;

        var notice = await db.ContractExtensionNotices.AsNoTracking().FirstOrDefaultAsync(n => n.Id == noticeId && n.ContractId == contractId, ct).ConfigureAwait(false);
        if (notice is null) return PmsError.NotFound("通知書不存在。");
        if (notice.PdfFileId is not { } fileId) return PmsError.NotFound("這份通知書為「僅記錄」，系統沒有存檔 PDF。");

        try
        {
            var download = await files.OpenDownloadAsync(fileId, FileAccessScope.Internal, ct).ConfigureAwait(false);
            if (download?.Content is null) return PmsError.NotFound("檔案不存在或已被清除。");

            using var buffer = new MemoryStream();
            await download.Content.CopyToAsync(buffer, ct).ConfigureAwait(false);
            return PmsResult<NoticePdfFile>.Success(new NoticePdfFile(buffer.ToArray(), notice.PdfFileName ?? $"{ctx.Value!.Contract.ContractNo}_通知書_{notice.Number}.pdf"));
        }
        catch (FileRejectedException ex)
        {
            return MapRejected(ex);
        }
    }

    private sealed record PreparedNotice(
        ContractCoopItem Item, int Delay, int Extension, DateOnly RequestedBy, DateOnly Today, DateOnly AsOf, int NextNumber, DateOnly? EffectiveDelivery);

    private async Task<PmsResult<PreparedNotice>> PrepareNoticeAsync(Contract c, Guid itemId, int? extensionWorkdays, DateOnly? requestedBy, CancellationToken ct)
    {
        var item = await db.ContractCoopItems.AsNoTracking().Include(i => i.Notices).FirstOrDefaultAsync(i => i.Id == itemId && i.ContractId == c.Id && !i.IsDeleted, ct).ConfigureAwait(false);
        if (item is null) return PmsError.NotFound("協力事項不存在。");
        if (item.Status != CoopStatus.Pending) return PmsError.Conflict("PMS.INVALID_TRANSITION", "這項協力事項已結束，不需要寄遲延通知。");

        var today = Today();
        var calendar = await CalendarAsync(today, ct).ConfigureAwait(false);
        var delay = ContractFulfillmentRules.DelayWorkdays(item.DueDate, item.CompletedDate, today, calendar);
        var extension = extensionWorkdays ?? Math.Max(1, delay);
        var request = requestedBy ?? calendar.AddWorkdays(today, 5);

        var notices = await db.ContractExtensionNotices.AsNoTracking().Where(n => n.ContractId == c.Id).Select(n => new { n.CoopItemId, n.ExtensionWorkdays, n.Number }).ToListAsync(ct).ConfigureAwait(false);
        var amendments = await db.ContractAmendments.AsNoTracking().Where(a => a.ContractId == c.Id).ToListAsync(ct).ConfigureAwait(false);
        var total = ContractFulfillmentRules.TotalExtensionWorkdays(
            notices.Select(n => (n.CoopItemId, n.ExtensionWorkdays)).Append((itemId, extension)));
        var effective = ContractFulfillmentRules.EffectiveDeliveryDate(c.DeliveryDate, amendments, total, calendar);

        return PmsResult<PreparedNotice>.Success(new PreparedNotice(item, delay, extension, request, today, today, (notices.Count == 0 ? 0 : notices.Max(n => n.Number)) + 1, effective));
    }

    private ExtensionNoticeData ToData(
        Contract c, PreparedNotice p, string actor, string? message, bool official, DateTimeOffset now, string? subject = null, string? bodyText = null,
        string? closing = null, string? signature = null) => new(
        c.ContractNo, c.Title, c.CustomerName, c.CustomerContactName, p.Item.Title, p.Item.Description, p.Item.DueDate, p.AsOf, p.Delay, p.Extension,
        p.EffectiveDelivery, p.RequestedBy, actor, configuration["ProjectManagement:CompanyName"] is { Length: > 0 } company ? company : "乙方", message, official, now,
        string.IsNullOrWhiteSpace(subject) ? null : subject, string.IsNullOrWhiteSpace(bodyText) ? null : bodyText,
        string.IsNullOrWhiteSpace(closing) ? null : closing, string.IsNullOrWhiteSpace(signature) ? null : signature);

    /// <summary>主旨與內文皆為選填（空白＝預設）；有填者驗證長度與主旨不得換行。</summary>
    private static string? ValidateOptionalText(string? subject, string? body)
    {
        if (!string.IsNullOrWhiteSpace(subject))
        {
            if (subject.Trim().Length > ContractFulfillmentRules.MaxSubjectLength) return $"主旨不得超過 {ContractFulfillmentRules.MaxSubjectLength} 字。";
            if (subject.Contains('\r') || subject.Contains('\n')) return "主旨不得換行。";
        }

        return !string.IsNullOrWhiteSpace(body) && body.Trim().Length > ContractFulfillmentRules.MaxBodyLength
            ? $"內文不得超過 {ContractFulfillmentRules.MaxBodyLength} 字。"
            : null;
    }

    /// <summary>通知書預設主旨與內文（依目前遲延、順延與請甲方提供期限產生），供編輯器帶入；任何合約成員可取得。</summary>
    public async Task<PmsResult<NoticeDefaultsDto>> GetNoticeDefaultsAsync(Guid userId, string actor, Guid contractId, Guid itemId, NoticePreviewRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadAsync(userId, contractId, track: false, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!.Contract;

        var prepared = await PrepareNoticeAsync(c, itemId, request.ExtensionWorkdays, request.RequestedBy, ct).ConfigureAwait(false);
        if (prepared.Error is { } preparedError) return preparedError;
        var p = prepared.Value!;

        var data = ToData(c, p, actor, message: null, official: false, clock.GetUtcNow());
        var calendar = await CalendarAsync(p.Today, ct).ConfigureAwait(false);
        var deadline = ContractFulfillmentRules.NoticeDeadline(p.Item.DueDate, calendar, ContractFulfillmentRules.EffectiveNoticeWorkdays(p.Item.NoticeWorkdays, c.CoopNoticeWorkdays));
        return PmsResult<NoticeDefaultsDto>.Success(new NoticeDefaultsDto(
            ContractExtensionNoticePdf.DefaultSubject(c.Title), ContractExtensionNoticePdf.DefaultBody(data), p.Delay, p.Extension, p.RequestedBy, p.EffectiveDelivery, deadline,
            ContractExtensionNoticePdf.DefaultClosing, ContractExtensionNoticePdf.DefaultSignature(data)));
    }

    // ---------- 文件審查回合 ----------

    public async Task<PmsResult<FulfillmentDto>> SubmitReviewRoundAsync(Guid userId, string actor, Guid contractId, SubmitReviewRoundRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, contractId, requirePerformance: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!.Contract;

        var deliverable = c.DeliverableItems.FirstOrDefault(i => i.Id == request.DeliverableItemId);
        if (deliverable is null) return PmsError.NotFound("交付項目不存在。");

        var existing = await db.ContractReviewRounds.Where(r => r.DeliverableItemId == deliverable.Id).ToListAsync(ct).ConfigureAwait(false);
        var invalid = ContractFulfillmentRules.ValidateNewRound(existing, deliverable.RequiresCustomerSignoff);
        if (invalid is not null) return PmsError.Conflict("PMS.INVALID_TRANSITION", invalid);
        if (existing.Count >= MaxRounds) return PmsError.Validation($"同一文件最多 {MaxRounds} 個審查回合。");

        var today = Today();
        var submitted = request.SubmittedDate ?? today;
        if (submitted > today) return PmsError.Validation("送審日不得晚於今天。");

        var calendar = await CalendarAsync(today, ct).ConfigureAwait(false);
        var roundNo = existing.Count + 1;
        var now = clock.GetUtcNow();
        var round = new ContractReviewRound
        {
            Id = Guid.NewGuid(), ContractId = contractId, DeliverableItemId = deliverable.Id, RoundNo = roundNo, SubmittedDate = submitted,
            ResponseDue = ContractFulfillmentRules.ReviewResponseDue(submitted, c.ReviewDeadlineWorkdays, calendar),
            ExceedsRoundLimit = ContractFulfillmentRules.ExceedsRoundLimit(roundNo, c.MaxReviewRounds), CreatedBy = actor, CreatedAtUtc = now
        };
        db.ContractReviewRounds.Add(round);

        var warn = round.ExceedsRoundLimit ? "（超出合約約定的彙整修改次數，超出部分須走 CR 另行計費）" : string.Empty;
        AddEvent(c, ContractEventType.ReviewRound,
            $"「{deliverable.Name}」第 {roundNo} 次送審{(round.ResponseDue is { } due ? $"，甲方回覆期限 {due:yyyy-MM-dd}" : "（合約未約定回覆期限）")}{warn}", userId, actor, now);
        return await SaveAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<FulfillmentDto>> RespondReviewRoundAsync(Guid userId, string actor, Guid contractId, Guid roundId, RespondReviewRoundRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, contractId, requirePerformance: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!.Contract;

        var round = await db.ContractReviewRounds.FirstOrDefaultAsync(r => r.Id == roundId && r.ContractId == contractId, ct).ConfigureAwait(false);
        if (round is null) return PmsError.NotFound("審查回合不存在。");
        if (request.Note is { Length: > 1000 }) return PmsError.Validation("備註不得超過 1000 字。");

        var today = Today();
        var responded = request.RespondedDate ?? today;
        var invalid = ContractFulfillmentRules.ValidateRespond(round.Outcome, request.Outcome, round.SubmittedDate, responded, today);
        if (invalid is not null) return PmsError.Validation(invalid);

        var now = clock.GetUtcNow();
        round.Outcome = request.Outcome;
        round.RespondedDate = responded;
        round.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        round.UpdatedBy = actor;
        round.UpdatedAtUtc = now;

        var name = c.DeliverableItems.FirstOrDefault(i => i.Id == round.DeliverableItemId)?.Name ?? "文件";
        AddEvent(c, ContractEventType.ReviewRound,
            $"「{name}」第 {round.RoundNo} 次送審：甲方{(request.Outcome == ReviewOutcome.Approved ? "簽認通過" : "提出修改意見")}（{responded:yyyy-MM-dd}）", userId, actor, now);
        return await SaveAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    // ---------- 驗收與擬制驗收 ----------

    public async Task<PmsResult<FulfillmentDto>> DeliverAsync(Guid userId, string actor, Guid contractId, DeliverRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, contractId, requirePerformance: false, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!.Contract;

        var acceptance = await db.ContractAcceptances.FirstOrDefaultAsync(a => a.ContractId == contractId, ct).ConfigureAwait(false);
        var today = Today();
        var invalid = ContractFulfillmentRules.ValidateDeliver(c.Status, acceptance?.Status ?? AcceptanceStatus.NotDelivered, request.DeliveredDate, today);
        if (invalid is not null) return PmsError.Conflict("PMS.INVALID_TRANSITION", invalid);
        if (request.Note is { Length: > 1000 }) return PmsError.Validation("備註不得超過 1000 字。");

        var calendar = await CalendarAsync(today, ct).ConfigureAwait(false);
        var now = clock.GetUtcNow();
        if (acceptance is null)
        {
            acceptance = new ContractAcceptance { Id = Guid.NewGuid(), ContractId = contractId, CreatedBy = actor, CreatedAtUtc = now };
            db.ContractAcceptances.Add(acceptance);
        }

        acceptance.Status = AcceptanceStatus.Delivered;
        acceptance.DeliveredDate = request.DeliveredDate;
        acceptance.DeemedDue = ContractFulfillmentRules.DeemedDue(request.DeliveredDate, c.DeemedAcceptanceWorkdays, calendar);
        acceptance.DefectReportedDate = null;
        acceptance.DefectNote = null;
        acceptance.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        acceptance.UpdatedBy = actor;
        acceptance.UpdatedAtUtc = now;

        AddEvent(c, ContractEventType.Acceptance,
            $"登錄交付（{request.DeliveredDate:yyyy-MM-dd}）{(acceptance.DeemedDue is { } due ? $"，擬制驗收期限 {due:yyyy-MM-dd}" : "；合約無擬制驗收條款")}", userId, actor, now);
        return await SaveAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<FulfillmentDto>> ReportDefectsAsync(Guid userId, string actor, Guid contractId, ReportDefectsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, contractId, requirePerformance: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!.Contract;

        var acceptance = await db.ContractAcceptances.FirstOrDefaultAsync(a => a.ContractId == contractId, ct).ConfigureAwait(false);
        if (acceptance?.DeliveredDate is not { } delivered) return PmsError.Conflict("PMS.INVALID_TRANSITION", "尚未登錄交付。");

        var invalid = ContractFulfillmentRules.ValidateDefects(acceptance.Status, acceptance.DeemedDue, delivered, request.ReportedDate, Today(), request.Note);
        if (invalid is not null) return PmsError.Conflict("PMS.INVALID_TRANSITION", invalid);

        var now = clock.GetUtcNow();
        acceptance.Status = AcceptanceStatus.DefectsReported;
        acceptance.DefectReportedDate = request.ReportedDate;
        acceptance.DefectNote = request.Note.Trim();
        acceptance.UpdatedBy = actor;
        acceptance.UpdatedAtUtc = now;
        AddEvent(c, ContractEventType.Acceptance, $"甲方於期限內提出具體瑕疵清單（{request.ReportedDate:yyyy-MM-dd}），轉瑕疵處理；修復後請重新登錄交付", userId, actor, now);
        return await SaveAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<FulfillmentDto>> ConfirmAcceptanceAsync(Guid userId, string actor, Guid contractId, ConfirmAcceptanceRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, contractId, requirePerformance: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!.Contract;

        var acceptance = await db.ContractAcceptances.FirstOrDefaultAsync(a => a.ContractId == contractId, ct).ConfigureAwait(false);
        var invalid = ContractFulfillmentRules.ValidateConfirm(acceptance?.Status ?? AcceptanceStatus.NotDelivered, acceptance?.DeliveredDate, request.AcceptedDate, Today());
        if (invalid is not null) return PmsError.Conflict("PMS.INVALID_TRANSITION", invalid);
        if (request.Note is { Length: > 1000 }) return PmsError.Validation("備註不得超過 1000 字。");

        var now = clock.GetUtcNow();
        acceptance!.Note = string.IsNullOrWhiteSpace(request.Note) ? acceptance.Note : request.Note.Trim();
        await ApplyAcceptedAsync(c, acceptance, AcceptanceStatus.Accepted, request.AcceptedDate, userId, actor, now, ct).ConfigureAwait(false);
        return await SaveAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    /// <summary>驗收完成（簽認或擬制）：更新驗收、合約進入已驗收、放行驗收後可請款的付款期別。由本服務與每日監看共用。</summary>
    internal async Task ApplyAcceptedAsync(
        Contract c, ContractAcceptance acceptance, AcceptanceStatus status, DateOnly date, Guid? userId, string actor, DateTimeOffset now, CancellationToken ct)
    {
        acceptance.Status = status;
        acceptance.AcceptedDate = date;
        acceptance.UpdatedBy = actor;
        acceptance.UpdatedAtUtc = now;

        var label = status == AcceptanceStatus.DeemedAccepted ? "擬制驗收（期限內未提具體瑕疵清單，依合約視為通過）" : "驗收完成（雙方簽認）";
        db.ContractEvents.Add(NewEvent(c.Id, ContractEventType.Acceptance, $"{label}：{date:yyyy-MM-dd}", userId ?? Guid.Empty, actor, now));

        if (c.Status == ContractStatus.Active)
        {
            c.Status = ContractStatus.Accepted;
            c.UpdatedBy = actor;
            c.UpdatedAtUtc = now;
            db.ContractEvents.Add(NewEvent(c.Id, ContractEventType.StatusChanged, "狀態 履約中 → 已驗收（保固起算）", userId ?? Guid.Empty, actor, now));
        }

        await PromotePaymentsAsync(c, accepted: true, actor, now, ct).ConfigureAwait(false);
    }

    // ---------- 合約變更 ----------

    public async Task<PmsResult<FulfillmentDto>> CreateAmendmentAsync(Guid userId, string actor, Guid contractId, CreateAmendmentRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, contractId, requirePerformance: true, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!.Contract;

        var amendments = await db.ContractAmendments.AsNoTracking().Where(a => a.ContractId == contractId).ToListAsync(ct).ConfigureAwait(false);
        var invalid = ContractFulfillmentRules.ValidateAmendment(request.Title, request.AmountDelta, request.NewDeliveryDate, request.WarrantyMonthsDelta,
            ContractFulfillmentRules.CurrentAmount(c.TotalAmount, amendments), request.Note);
        if (invalid is not null) return PmsError.Validation(invalid);

        var today = Today();
        var now = clock.GetUtcNow();
        var amendment = new ContractAmendment
        {
            Id = Guid.NewGuid(), ContractId = contractId, Source = AmendmentSource.Manual, Title = request.Title.Trim(), AmountDelta = request.AmountDelta,
            NewDeliveryDate = request.NewDeliveryDate, WarrantyMonthsDelta = request.WarrantyMonthsDelta, EffectiveDate = request.EffectiveDate ?? today,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(), CreatedBy = actor, CreatedAtUtc = now
        };
        db.ContractAmendments.Add(amendment);
        AddEvent(c, ContractEventType.Amendment, $"登錄合約變更「{amendment.Title}」（{DescribeAmendment(amendment)}）", userId, actor, now);
        return await SaveAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    /// <summary>刪除手動登錄的變更（CR 自動掛帳的變更不可刪除；若 CR 掛帳有誤請以相反的手動變更沖銷）。已有請款時不可刪除。</summary>
    public async Task<PmsResult<FulfillmentDto>> DeleteAmendmentAsync(Guid userId, string actor, Guid contractId, Guid amendmentId, CancellationToken ct)
    {
        var ctx = await LoadManageAsync(userId, contractId, requirePerformance: false, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!.Contract;

        var amendment = await db.ContractAmendments.FirstOrDefaultAsync(a => a.Id == amendmentId && a.ContractId == contractId, ct).ConfigureAwait(false);
        if (amendment is null) return PmsError.NotFound("合約變更不存在。");
        if (amendment.Source != AmendmentSource.Manual) return PmsError.Conflict("PMS.CONTENT_LOCKED", "CR 自動掛帳的變更不可刪除；若有誤請登錄相反的手動變更沖銷。");
        if (await db.ContractPaymentTerms.AnyAsync(p => p.ContractId == contractId && p.Status >= PaymentStatus.Invoiced, ct).ConfigureAwait(false))
            return PmsError.Conflict("PMS.CONTENT_LOCKED", "已有請款紀錄，現行金額已被引用，不可刪除變更；請登錄相反的手動變更沖銷。");

        var now = clock.GetUtcNow();
        db.ContractAmendments.Remove(amendment);
        AddEvent(c, ContractEventType.Amendment, $"刪除手動合約變更「{amendment.Title}」", userId, actor, now);
        return await SaveAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    // ---------- 付款期程 ----------

    public async Task<PmsResult<FulfillmentDto>> SetPaymentsAsync(Guid userId, string actor, Guid contractId, SetPaymentsRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, contractId, requirePerformance: false, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!.Contract;

        if (!IsSigned(c.Status)) return PmsError.Conflict("PMS.INVALID_TRANSITION", "合約簽約後才能設定付款期程。");

        var existing = await db.ContractPaymentTerms.Where(p => p.ContractId == contractId).ToListAsync(ct).ConfigureAwait(false);
        var terms = (request.Terms ?? []).Select(t => (t.Name ?? string.Empty, t.Percent, t.Trigger)).ToList();
        var check = ContractFulfillmentRules.ValidateSchedule(terms, existing.Any(p => p.Status >= PaymentStatus.Invoiced));
        if (!check.Ok) return PmsError.Validation(check.Error!);

        var acceptance = await db.ContractAcceptances.AsNoTracking().FirstOrDefaultAsync(a => a.ContractId == contractId, ct).ConfigureAwait(false);
        var accepted = acceptance is not null && ContractFulfillmentRules.IsAccepted(acceptance.Status);

        var now = clock.GetUtcNow();
        db.ContractPaymentTerms.RemoveRange(existing);
        var order = 0;
        foreach (var t in request.Terms!)
        {
            var term = new ContractPaymentTerm
            {
                Id = Guid.NewGuid(), ContractId = contractId, SortOrder = ++order, Name = t.Name.Trim(), Percent = t.Percent, Trigger = t.Trigger,
                PlannedDate = t.PlannedDate, CreatedBy = actor, CreatedAtUtc = now
            };
            term.Status = ContractFulfillmentRules.AutoStatus(term, c.Status, accepted);
            db.ContractPaymentTerms.Add(term);
        }

        AddEvent(c, ContractEventType.Payment, $"設定付款期程（{request.Terms!.Count} 期）", userId, actor, now);
        return await SaveAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    public async Task<PmsResult<FulfillmentDto>> MovePaymentAsync(Guid userId, string actor, Guid contractId, Guid paymentId, MovePaymentRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ctx = await LoadManageAsync(userId, contractId, requirePerformance: false, ct).ConfigureAwait(false);
        if (ctx.Error is { } error) return error;
        var c = ctx.Value!.Contract;

        var term = await db.ContractPaymentTerms.FirstOrDefaultAsync(p => p.Id == paymentId && p.ContractId == contractId, ct).ConfigureAwait(false);
        if (term is null) return PmsError.NotFound("付款期別不存在。");
        if (!Enum.IsDefined(request.Status)) return PmsError.Validation("付款狀態不正確。");

        var acceptance = await db.ContractAcceptances.AsNoTracking().FirstOrDefaultAsync(a => a.ContractId == contractId, ct).ConfigureAwait(false);
        var accepted = acceptance is not null && ContractFulfillmentRules.IsAccepted(acceptance.Status);
        var today = Today();
        var invalid = ContractFulfillmentRules.ValidateMove(term, request.Status, c.Status, accepted, request.InvoiceNo, request.Date, today);
        if (invalid is not null) return PmsError.Conflict("PMS.INVALID_TRANSITION", invalid);

        var now = clock.GetUtcNow();
        var from = term.Status;
        switch (request.Status)
        {
            case PaymentStatus.Invoiced:
                var amendments = await db.ContractAmendments.AsNoTracking().Where(a => a.ContractId == contractId).ToListAsync(ct).ConfigureAwait(false);
                var all = await db.ContractPaymentTerms.AsNoTracking().Where(p => p.ContractId == contractId).OrderBy(p => p.SortOrder).ToListAsync(ct).ConfigureAwait(false);
                var amounts = ContractFulfillmentRules.Amounts(ContractFulfillmentRules.CurrentAmount(c.TotalAmount, amendments), all.Select(p => p.Percent).ToList());
                term.InvoicedAmount = amounts[all.FindIndex(p => p.Id == term.Id)];
                term.InvoiceNo = request.InvoiceNo!.Trim();
                term.InvoicedDate = request.Date;
                break;
            case PaymentStatus.Paid:
                term.PaidDate = request.Date;
                break;
        }

        term.Status = request.Status;
        term.UpdatedBy = actor;
        term.UpdatedAtUtc = now;
        AddEvent(c, ContractEventType.Payment, $"付款期別「{term.Name}」{ContractFulfillmentRules.StatusName(from)} → {ContractFulfillmentRules.StatusName(request.Status)}", userId, actor, now);
        return await SaveAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    /// <summary>依合約狀態與驗收進度自動放行付款期別（簽約觸發、驗收觸發）；回傳放行期數。</summary>
    internal async Task<int> PromotePaymentsAsync(Contract c, bool accepted, string actor, DateTimeOffset now, CancellationToken ct)
    {
        var terms = await db.ContractPaymentTerms.Where(p => p.ContractId == c.Id && p.Status == PaymentStatus.Pending).ToListAsync(ct).ConfigureAwait(false);
        var promoted = 0;
        foreach (var term in terms)
        {
            var next = ContractFulfillmentRules.AutoStatus(term, c.Status, accepted);
            if (next == term.Status) continue;

            term.Status = next;
            term.UpdatedBy = actor;
            term.UpdatedAtUtc = now;
            promoted++;
            db.ContractEvents.Add(NewEvent(c.Id, ContractEventType.Payment, $"付款期別「{term.Name}」已達請款條件（可請款）", Guid.Empty, actor, now));
        }

        return promoted;
    }

    // ---------- 內部輔助 ----------

    private sealed record Ctx(Contract Contract, IReadOnlyList<(Guid UserId, ContractMemberRole Role)> Members);

    private static bool IsPerformance(ContractStatus s) => s is ContractStatus.Active or ContractStatus.Accepted or ContractStatus.Warranty;

    private static bool IsSigned(ContractStatus s) => s is ContractStatus.Active or ContractStatus.Accepted or ContractStatus.Warranty or ContractStatus.Closed;

    private async Task<PmsResult<Ctx>> LoadAsync(Guid userId, Guid contractId, bool track, CancellationToken ct)
    {
        var query = db.Contracts.AsQueryable();
        if (!track) query = query.AsNoTracking();

        var contract = await query.Include(c => c.Members).Include(c => c.DeliverableItems).AsSplitQuery().FirstOrDefaultAsync(c => c.Id == contractId, ct).ConfigureAwait(false);
        var members = contract?.Members.Select(m => (m.UserId, m.Role)).ToList();
        if (contract is null || members is null || !ContractRules.IsMember(userId, members)) return PmsError.NotFound("合約不存在。");

        return PmsResult<Ctx>.Success(new Ctx(contract, members));
    }

    private async Task<PmsResult<Ctx>> LoadManageAsync(Guid userId, Guid contractId, bool requirePerformance, CancellationToken ct)
    {
        var ctx = await LoadAsync(userId, contractId, track: true, ct).ConfigureAwait(false);
        if (ctx.Error is not null) return ctx;

        if (!ContractRules.CanManage(userId, ctx.Value!.Members)) return PmsError.Forbidden("僅合約的業務或合約 PM 可執行此操作。");
        if (requirePerformance && !IsPerformance(ctx.Value.Contract.Status))
            return PmsError.Conflict("PMS.INVALID_TRANSITION", "合約須在履約階段（履約中、已驗收、保固中）才能執行此操作。");
        if (!requirePerformance && !IsSigned(ctx.Value.Contract.Status))
            return PmsError.Conflict("PMS.INVALID_TRANSITION", "合約簽約後才能執行此操作。");

        return ctx;
    }

    private async Task<PmsResult<FulfillmentDto>> SaveAsync(Guid userId, Guid contractId, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "資料已被他人更新，請重新載入後再試。");
        }
        catch (DbUpdateException ex)
        {
            db.ChangeTracker.Clear();
            logger.LogWarning(ex, "[PMS] 合約履約資料儲存失敗：{ContractId}", contractId);
            return PmsError.Conflict("PMS.CONCURRENCY_CONFLICT", "資料剛被他人更新或重複，請重新載入後再試。");
        }

        db.ChangeTracker.Clear();
        return await GetAsync(userId, contractId, ct).ConfigureAwait(false);
    }

    private async Task CompensateAsync(Guid fileId)
    {
        try
        {
            await files.DeleteAsync(fileId, FileAccessScope.Internal, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[PMS] 展延通知書寄送失敗，且回收 FMS 檔案 {FileId} 失敗，請維運清理孤兒檔案。", fileId);
        }
    }

    private void AddEvent(Contract c, ContractEventType type, string message, Guid userId, string actor, DateTimeOffset at) =>
        db.ContractEvents.Add(NewEvent(c.Id, type, message, userId, actor, at));

    internal static ContractEvent NewEvent(Guid contractId, ContractEventType type, string message, Guid userId, string actor, DateTimeOffset at) =>
        new() { Id = Guid.NewGuid(), ContractId = contractId, Type = type, Message = message.Length > 1000 ? message[..1000] : message, ActorUserId = userId, ActorName = actor, At = at };

    private static string DescribeAmendment(ContractAmendment a)
    {
        var parts = new List<string>();
        if (a.AmountDelta != 0) parts.Add($"金額 {(a.AmountDelta > 0 ? "+" : string.Empty)}{a.AmountDelta:N0}");
        if (a.NewDeliveryDate is { } d) parts.Add($"新交付日 {d:yyyy-MM-dd}");
        if (a.WarrantyMonthsDelta != 0) parts.Add($"保固 {(a.WarrantyMonthsDelta > 0 ? "+" : string.Empty)}{a.WarrantyMonthsDelta} 個月");
        return string.Join("、", parts);
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(SlaCalculator.Offset).DateTime);

    private Task<WorkCalendar> CalendarAsync(DateOnly center, CancellationToken ct) =>
        holidays.LoadCalendarAsync(center.AddDays(-730), center.AddDays(730), ct);

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
