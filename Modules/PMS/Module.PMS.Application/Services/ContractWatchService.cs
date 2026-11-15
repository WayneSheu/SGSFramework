using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// 合約履約每日監看（pms-contract-watch，工作日 09:00）：
/// ①甲方協力事項逾期仍未通知／通知期限將到 → 提醒；②文件審查逾期未回覆且合約約定視為通過 → 自動標「擬制通過」；
/// ③已交付且超過擬制驗收期限、無瑕疵清單 → 自動「擬制驗收」並放行尾款；④付款期別到達請款條件 → 放行；已放行且超過預計請款日 → 提醒。
/// 每份合約每天最多一封彙整信（寄給業務與合約 PM）；同一事項每個台灣日最多提醒一次；非工作日不執行。
/// 狀態自動標記（擬制通過、擬制驗收）不依賴郵件是否寄出成功。
/// </summary>
public sealed class ContractWatchService(
    PMSDbContext db,
    HolidayService holidays,
    ContractFulfillmentService fulfillment,
    PmsMailNotifier notifier,
    TimeProvider clock,
    ILogger<ContractWatchService> logger)
{
    private const string Actor = "系統排程（pms-contract-watch）";

    /// <summary>擬制驗收到期前幾個工作日開始提醒。</summary>
    private const int AcceptanceWarnWorkdays = 3;

    private static readonly ContractStatus[] Watched = [ContractStatus.Active, ContractStatus.Accepted, ContractStatus.Warranty];

    public async Task<ContractWatchResult> ScanAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.ToOffset(SlaCalculator.Offset).DateTime);
        var calendar = await holidays.LoadCalendarAsync(today.AddDays(-730), today.AddDays(730), ct).ConfigureAwait(false);
        if (!calendar.IsWorkday(today))
            return new ContractWatchResult(0, 0, 0, 0, 0, 0, "今天不是工作日，略過合約履約監看。");

        var contracts = await db.Contracts.Where(c => Watched.Contains(c.Status)).Include(c => c.Members).AsSplitQuery().ToListAsync(ct).ConfigureAwait(false);

        int coop = 0, deemedReviews = 0, deemedAcceptances = 0, promoted = 0, remindedPayments = 0, remindedWarranty = 0, remindedDeliverables = 0, skipped = 0;

        foreach (var contract in contracts)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var lines = new List<string>();
                var remindOnSend = new List<Action>();

                // ① 協力事項
                var items = await db.ContractCoopItems.Include(i => i.Notices)
                    .Where(i => i.ContractId == contract.Id && !i.IsDeleted && i.Status == CoopStatus.Pending && i.DueDate < today).ToListAsync(ct).ConfigureAwait(false);
                foreach (var item in items)
                {
                    var latest = item.Notices.Count == 0 ? 0 : item.Notices.Max(n => n.ExtensionWorkdays);
                    var delay = ContractFulfillmentRules.DelayWorkdays(item.DueDate, null, today, calendar);
                    if (!ContractFulfillmentRules.NeedsNotice(item, delay, latest) || RemindedToday(item.LastReminderAt, now)) continue;

                    var noticeDays = ContractFulfillmentRules.EffectiveNoticeWorkdays(item.NoticeWorkdays, contract.CoopNoticeWorkdays);
                    var deadline = ContractFulfillmentRules.NoticeDeadline(item.DueDate, calendar, noticeDays);
                    var state = ContractFulfillmentRules.State(item, item.Notices.Count > 0, today, calendar, noticeDays);
                    lines.Add($"甲方協力事項「{item.Title}」已逾期 {delay} 個工作日（應完成日 {item.DueDate:yyyy-MM-dd}）。" +
                              (item.Notices.Count == 0
                                  ? (state == CoopState.LateNoticeOverdue ? $"⚠ 書面通知期限（{deadline:yyyy-MM-dd}）已過，請盡速寄出《協力遲延與工期展延通知書》。" : $"請於 {deadline:yyyy-MM-dd} 前寄出《協力遲延與工期展延通知書》。")
                                  : $"遲延已超過最近一次通知的順延工作日（{latest}），建議補寄更新版通知書。"));
                    remindOnSend.Add(() => item.LastReminderAt = now);
                    coop++;
                }

                // ② 文件審查擬制通過
                var pendingRounds = await db.ContractReviewRounds.Include(r => r.DeliverableItem)
                    .Where(r => r.ContractId == contract.Id && r.Outcome == ReviewOutcome.Pending && r.ResponseDue != null && r.ResponseDue < today).ToListAsync(ct).ConfigureAwait(false);
                foreach (var round in pendingRounds.Where(r => ContractFulfillmentRules.ShouldDeemApproved(r, contract.DeemedPassOnTimeout, today)))
                {
                    round.Outcome = ReviewOutcome.DeemedApproved;
                    round.RespondedDate = today;
                    round.Note = "逾期未回覆，依合約視為通過（系統自動標記）";
                    round.UpdatedBy = Actor;
                    round.UpdatedAtUtc = now;
                    var name = round.DeliverableItem?.Name ?? "文件";
                    db.ContractEvents.Add(ContractFulfillmentService.NewEvent(contract.Id, ContractEventType.ReviewRound,
                        $"「{name}」第 {round.RoundNo} 次送審：甲方逾期（{round.ResponseDue:yyyy-MM-dd}）未回覆，擬制通過", Guid.Empty, Actor, now));
                    lines.Add($"「{name}」第 {round.RoundNo} 次送審已超過甲方回覆期限（{round.ResponseDue:yyyy-MM-dd}），依合約自動標記為「擬制通過」。");
                    deemedReviews++;
                }

                // ③ 擬制驗收
                var acceptance = await db.ContractAcceptances.FirstOrDefaultAsync(a => a.ContractId == contract.Id, ct).ConfigureAwait(false);
                if (acceptance is not null)
                {
                    if (ContractFulfillmentRules.ShouldDeemAccepted(acceptance, today))
                    {
                        await fulfillment.ApplyAcceptedAsync(contract, acceptance, AcceptanceStatus.DeemedAccepted, acceptance.DeemedDue!.Value, null, Actor, now, ct).ConfigureAwait(false);
                        lines.Add($"已交付（{acceptance.DeliveredDate:yyyy-MM-dd}）且超過擬制驗收期限（{acceptance.DeemedDue:yyyy-MM-dd}）、甲方未提具體瑕疵清單，依合約自動標記為「擬制驗收」，保固起算，驗收後可請款的期別已放行。");
                        deemedAcceptances++;
                    }
                    else if (acceptance.Status == AcceptanceStatus.Delivered && acceptance.DeemedDue is { } due && today <= due && !RemindedToday(acceptance.LastReminderAt, now)
                             && ContractFulfillmentRules.WorkdaysBetween(today, due, calendar) <= AcceptanceWarnWorkdays)
                    {
                        lines.Add($"擬制驗收期限將於 {due:yyyy-MM-dd} 到期（剩 {ContractFulfillmentRules.WorkdaysBetween(today, due, calendar)} 個工作日）；若甲方有具體瑕疵清單，請在期限內記錄，否則屆時視為驗收通過。");
                        remindOnSend.Add(() => acceptance.LastReminderAt = now);
                    }
                }

                // ③-2 保固：驗收完成後進入保固期；到期前 60／30／7 天與到期後各提醒一次
                var accepted = acceptance is not null && ContractFulfillmentRules.IsAccepted(acceptance.Status);
                if (accepted && acceptance!.AcceptedDate is { } acceptedOn)
                {
                    var amendments = await db.ContractAmendments.AsNoTracking().Where(a => a.ContractId == contract.Id).ToListAsync(ct).ConfigureAwait(false);
                    var months = ContractFulfillmentRules.CurrentWarrantyMonths(contract.WarrantyMonths, amendments);

                    if (contract.Status == ContractStatus.Accepted && months > 0)
                    {
                        contract.Status = ContractStatus.Warranty;
                        contract.UpdatedBy = Actor;
                        contract.UpdatedAtUtc = now;
                        db.ContractEvents.Add(ContractFulfillmentService.NewEvent(contract.Id, ContractEventType.Warranty,
                            $"保固期開始：自 {acceptedOn:yyyy-MM-dd} 起 {months} 個月（至 {acceptedOn.AddMonths(months):yyyy-MM-dd}）", Guid.Empty, Actor, now));
                    }

                    if (ContractWarrantyRules.WarrantyEnd(acceptedOn, months) is { } warrantyEnd)
                    {
                        var daysLeft = ContractWarrantyRules.DaysLeft(warrantyEnd, today);
                        var stage = ContractWarrantyRules.StageFor(daysLeft);
                        if (ContractWarrantyRules.ShouldRemind(stage, acceptance.WarrantyReminderStage) && contract.Status is ContractStatus.Warranty or ContractStatus.Accepted)
                        {
                            lines.Add(ContractWarrantyRules.Message(stage, warrantyEnd, daysLeft));
                            remindOnSend.Add(() => acceptance.WarrantyReminderStage = stage);
                            remindedWarranty++;
                        }
                    }
                }

                // ③-3 附件一交付項目：到期前 3 個工作日內與逾期仍未記錄交付者提醒（每項每個台灣日最多一次）
                var pendingDeliverables = await db.ContractDeliverableItems
                    .Where(i => i.ContractId == contract.Id && i.DeliveryDate != null && i.DeliveredDate == null).OrderBy(i => i.SortOrder).ToListAsync(ct).ConfigureAwait(false);
                foreach (var d in pendingDeliverables)
                {
                    var dState = ContractDeliverableRules.StateOf(d.DeliveryDate, d.DeliveredDate, today, calendar);
                    if (dState is not (DeliverableState.DueSoon or DeliverableState.Overdue) || RemindedToday(d.LastReminderAt, now)) continue;

                    lines.Add(ContractDeliverableRules.ReminderMessage(d.Name, d.DeliveryDate!.Value, dState, today, calendar));
                    remindOnSend.Add(() => d.LastReminderAt = now);
                    remindedDeliverables++;
                }

                // ④ 付款期別放行與提醒
                var newlyPromoted = await fulfillment.PromotePaymentsAsync(contract, accepted, Actor, now, ct).ConfigureAwait(false);
                if (newlyPromoted > 0) lines.Add($"{newlyPromoted} 個付款期別已達請款條件，請開立發票並於系統登記。");
                promoted += newlyPromoted;

                var duePayments = await db.ContractPaymentTerms
                    .Where(p => p.ContractId == contract.Id && p.Status == PaymentStatus.Invoiceable && p.PlannedDate != null && p.PlannedDate <= today).ToListAsync(ct).ConfigureAwait(false);
                foreach (var term in duePayments.Where(p => !RemindedToday(p.LastReminderAt, now)))
                {
                    lines.Add($"付款期別「{term.Name}」已可請款，預計請款日 {term.PlannedDate:yyyy-MM-dd} 已到，尚未登記請款。");
                    remindOnSend.Add(() => term.LastReminderAt = now);
                    remindedPayments++;
                }

                await db.SaveChangesAsync(ct).ConfigureAwait(false);   // 自動標記的狀態先落地，不受郵件結果影響

                if (lines.Count == 0) continue;

                var recipients = await notifier.ResolveEmailsAsync(
                    contract.Members.Where(m => m.Role is ContractMemberRole.Pm or ContractMemberRole.Sales).Select(m => m.UserId), ct).ConfigureAwait(false);
                if (recipients.Count == 0)
                {
                    skipped++;
                    logger.LogWarning("[PMS] 合約 {ContractNo} 有履約提醒事項，但找不到業務／合約 PM 的 Email，未寄出。", contract.ContractNo);
                    continue;
                }

                var sent = await notifier.SendAsync(
                    $"[合約 {contract.ContractNo}] 履約提醒（{lines.Count} 項）", Body(contract, lines, today), recipients,
                    "PMS 合約履約提醒", $"pms.contract.watch:{contract.Id:N}:{today:yyyyMMdd}", now, ct).ConfigureAwait(false);
                if (!sent)
                {
                    skipped++;
                    continue;
                }

                foreach (var mark in remindOnSend) mark();
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                db.ChangeTracker.Clear();
                skipped++;
                logger.LogError(ex, "[PMS] 合約 {ContractNo} 履約監看失敗。", contract.ContractNo);
            }
        }

        var summary = $"監看 {contracts.Count} 份合約：協力提醒 {coop}、擬制通過 {deemedReviews}、擬制驗收 {deemedAcceptances}、保固提醒 {remindedWarranty}、交付到期提醒 {remindedDeliverables}、付款放行 {promoted}、請款提醒 {remindedPayments}"
                      + (skipped > 0 ? $"；{skipped} 份因無收件人、寄送失敗或處理失敗未提醒，下次再試" : null);
        logger.LogInformation("[PMS] 合約履約監看：{Summary}", summary);
        return new ContractWatchResult(coop, deemedReviews, deemedAcceptances, promoted, remindedPayments, skipped, summary, remindedWarranty, remindedDeliverables);
    }

    private static bool RemindedToday(DateTimeOffset? last, DateTimeOffset now) =>
        last is { } at && DateOnly.FromDateTime(at.ToOffset(SlaCalculator.Offset).DateTime) == DateOnly.FromDateTime(now.ToOffset(SlaCalculator.Offset).DateTime);

    private static string Body(Contract c, IReadOnlyList<string> lines, DateOnly today)
    {
        static string E(string s) => WebUtility.HtmlEncode(s);

        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family: 'Microsoft JhengHei', Arial, sans-serif; font-size: 14px; line-height: 1.7; color: #212529;\">");
        sb.Append("<p>合約 <strong>").Append(E(c.ContractNo)).Append("</strong>（").Append(E(c.Title)).Append("／").Append(E(c.CustomerName))
          .Append("）截至 ").Append(today.ToString("yyyy-MM-dd")).Append(" 有下列履約事項需要處理：</p><ul>");
        foreach (var line in lines) sb.Append("<li>").Append(E(line)).Append("</li>");
        sb.Append("</ul><p>請至「專案合約」的「履約管理」頁處理。</p></div>");
        return sb.ToString();
    }
}

/// <summary>CR 核准時自動掛帳到合約（追加金額、順延上線日）：專案須已綁定履約階段的合約；同一張 CR 只掛帳一次。</summary>
public static class ContractCrHook
{
    /// <summary>
    /// 在 CR 核准的同一個交易中呼叫（不自行儲存）。客戶 CR 的報價金額視為追加金額；決策方案 A（順延上線日）的新上線日視為新的合約交付日；
    /// 方案 C（拒絕）、無金額且無新日期者不掛帳。
    /// </summary>
    public static async Task TryRecordAsync(PMSDbContext db, ChangeRequest cr, string actor, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(cr);

        if (cr.Decision is null or CrDecision.C_Reject) return;

        var amount = cr.Kind == CrKind.Customer ? cr.QuoteAmount ?? 0m : 0m;
        var newDate = cr.Decision == CrDecision.A_Delay ? cr.NewReleaseDate : null;
        if (amount == 0m && newDate is null) return;

        var contract = await db.ContractProjects.Where(l => l.ProjectId == cr.ProjectId)
            .Select(l => new { l.ContractId, l.Contract!.Status, l.Contract.ContractNo }).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (contract is null || contract.Status is not (ContractStatus.Active or ContractStatus.Accepted or ContractStatus.Warranty)) return;
        if (await db.ContractAmendments.AnyAsync(a => a.ChangeRequestId == cr.Id, ct).ConfigureAwait(false)) return;

        var today = DateOnly.FromDateTime(now.ToOffset(SlaCalculator.Offset).DateTime);
        var title = $"CR {cr.CrNo} {cr.Title}";
        db.ContractAmendments.Add(new ContractAmendment
        {
            Id = Guid.NewGuid(), ContractId = contract.ContractId, Source = AmendmentSource.ChangeRequest, ChangeRequestId = cr.Id, CrNo = cr.CrNo,
            Title = title.Length > 200 ? title[..200] : title, AmountDelta = amount, NewDeliveryDate = newDate, EffectiveDate = today,
            Note = "CR 簽核齊備後自動掛帳", CreatedBy = actor, CreatedAtUtc = now
        });

        var parts = new List<string>();
        if (amount != 0) parts.Add($"追加 {amount:N0} 元");
        if (newDate is { } d) parts.Add($"新交付日 {d:yyyy-MM-dd}");
        db.ContractEvents.Add(new ContractEvent
        {
            Id = Guid.NewGuid(), ContractId = contract.ContractId, Type = ContractEventType.Amendment,
            Message = $"CR {cr.CrNo} 核准，自動掛帳合約變更：{string.Join("、", parts)}", ActorUserId = Guid.Empty, ActorName = actor, At = now
        });
    }
}
