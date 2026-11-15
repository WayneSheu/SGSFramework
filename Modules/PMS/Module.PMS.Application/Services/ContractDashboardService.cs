using Microsoft.EntityFrameworkCore;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// 合約儀表：我參與的合約彙總——進行中合約與現行金額、待我核決、協力遲延（含通知期限已過）、保固將到期（60 天內）、
/// 可請款未請款、擬制驗收將到期（3 個工作日內），並列出需要處理的提醒清單（最多 20 筆，嚴重者在前）。僅統計我是成員的合約。
/// </summary>
public sealed class ContractDashboardService(PMSDbContext db, HolidayService holidays, ContractService contracts, TimeProvider clock)
{
    private static readonly ContractStatus[] Performance = [ContractStatus.Active, ContractStatus.Accepted, ContractStatus.Warranty];
    private const int MaxAlerts = 20;
    private const int AcceptanceWarnWorkdays = 3;
    private const int WarrantyWarnDays = 60;

    public async Task<ContractDashboardDto> GetAsync(Guid userId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(SlaCalculator.Offset).DateTime);
        var calendar = await holidays.LoadCalendarAsync(today.AddDays(-730), today.AddDays(730), ct).ConfigureAwait(false);

        var mine = await db.Contracts.AsNoTracking().Where(c => Performance.Contains(c.Status) && c.Members.Any(m => m.UserId == userId))
            .Select(c => new { c.Id, c.ContractNo, c.Title, c.Status, c.TotalAmount, c.WarrantyMonths, c.CoopNoticeWorkdays }).ToListAsync(ct).ConfigureAwait(false);
        var ids = mine.Select(c => c.Id).ToList();

        var amendments = await db.ContractAmendments.AsNoTracking().Where(a => ids.Contains(a.ContractId)).ToListAsync(ct).ConfigureAwait(false);
        var amendmentsBy = amendments.GroupBy(a => a.ContractId).ToDictionary(g => g.Key, g => (IReadOnlyList<ContractAmendment>)g.ToList());
        var current = mine.ToDictionary(c => c.Id, c => ContractFulfillmentRules.CurrentAmount(c.TotalAmount, amendmentsBy.GetValueOrDefault(c.Id) ?? []));

        var alerts = new List<DashboardAlertDto>();

        // 協力事項逾期
        var coop = await db.ContractCoopItems.AsNoTracking().Include(i => i.Notices)
            .Where(i => ids.Contains(i.ContractId) && !i.IsDeleted && i.Status == CoopStatus.Pending && i.DueDate < today).ToListAsync(ct).ConfigureAwait(false);
        int coopOverdue = 0, coopNoticeOverdue = 0;
        foreach (var item in coop)
        {
            var contract = mine.First(c => c.Id == item.ContractId);
            var days = ContractFulfillmentRules.EffectiveNoticeWorkdays(item.NoticeWorkdays, contract.CoopNoticeWorkdays);
            var state = ContractFulfillmentRules.State(item, item.Notices.Count > 0, today, calendar, days);
            coopOverdue++;
            if (state == CoopState.LateNoticeOverdue)
            {
                coopNoticeOverdue++;
                alerts.Add(new(contract.Id, contract.ContractNo, contract.Title, $"協力事項「{item.Title}」逾期且書面通知期限已過，尚未寄出通知書", ContractFulfillmentRules.NoticeDeadline(item.DueDate, calendar, days), "danger"));
            }
            else if (state == CoopState.LateNoticeDue)
            {
                alerts.Add(new(contract.Id, contract.ContractNo, contract.Title, $"協力事項「{item.Title}」已逾期，請於通知期限前寄出遲延通知書", ContractFulfillmentRules.NoticeDeadline(item.DueDate, calendar, days), "warning"));
            }
        }

        // 保固與擬制驗收
        var acceptances = await db.ContractAcceptances.AsNoTracking().Where(a => ids.Contains(a.ContractId)).ToListAsync(ct).ConfigureAwait(false);
        int warrantyExpiring = 0, acceptanceSoon = 0;
        foreach (var a in acceptances)
        {
            var contract = mine.First(c => c.Id == a.ContractId);

            if (a.Status == AcceptanceStatus.Delivered && a.DeemedDue is { } due && due >= today && ContractFulfillmentRules.WorkdaysBetween(today, due, calendar) <= AcceptanceWarnWorkdays)
            {
                acceptanceSoon++;
                alerts.Add(new(contract.Id, contract.ContractNo, contract.Title, "擬制驗收期限將到，甲方若有具體瑕疵清單須在期限內記錄，否則視為驗收通過", due, "warning"));
            }

            if (ContractFulfillmentRules.IsAccepted(a.Status) && a.AcceptedDate is { } accepted)
            {
                var months = ContractFulfillmentRules.CurrentWarrantyMonths(contract.WarrantyMonths, amendmentsBy.GetValueOrDefault(contract.Id) ?? []);
                if (ContractWarrantyRules.WarrantyEnd(accepted, months) is { } end)
                {
                    var left = ContractWarrantyRules.DaysLeft(end, today);
                    if (left >= 0 && left <= WarrantyWarnDays)
                    {
                        warrantyExpiring++;
                        alerts.Add(new(contract.Id, contract.ContractNo, contract.Title, $"保固將於 {end:yyyy-MM-dd} 到期（剩 {left} 天）", end, left <= 7 ? "danger" : "warning"));
                    }
                }
            }
        }

        // 可請款
        var payments = await db.ContractPaymentTerms.AsNoTracking().Where(p => ids.Contains(p.ContractId)).OrderBy(p => p.SortOrder).ToListAsync(ct).ConfigureAwait(false);
        int invoiceableCount = 0;
        decimal invoiceableAmount = 0;
        foreach (var group in payments.GroupBy(p => p.ContractId))
        {
            var ordered = group.OrderBy(p => p.SortOrder).ToList();
            var amounts = ContractFulfillmentRules.Amounts(current[group.Key], ordered.Select(p => p.Percent).ToList());
            for (var i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Status != PaymentStatus.Invoiceable) continue;

                invoiceableCount++;
                invoiceableAmount += amounts[i];
                var contract = mine.First(c => c.Id == group.Key);
                if (ordered[i].PlannedDate is { } planned && planned <= today)
                    alerts.Add(new(contract.Id, contract.ContractNo, contract.Title, $"付款期別「{ordered[i].Name}」已可請款且預計請款日已到，尚未登記請款", planned, "warning"));
            }
        }

        // 附件一交付項目到期未交付
        var deliverables = await db.ContractDeliverableItems.AsNoTracking()
            .Where(i => ids.Contains(i.ContractId) && i.DeliveryDate != null && i.DeliveredDate == null).OrderBy(i => i.DeliveryDate).ToListAsync(ct).ConfigureAwait(false);
        var deliverablesOverdue = 0;
        foreach (var item in deliverables)
        {
            var dState = ContractDeliverableRules.StateOf(item.DeliveryDate, item.DeliveredDate, today, calendar);
            if (dState is not (DeliverableState.DueSoon or DeliverableState.Overdue)) continue;

            var contract = mine.First(c => c.Id == item.ContractId);
            if (dState == DeliverableState.Overdue) deliverablesOverdue++;
            alerts.Add(new(contract.Id, contract.ContractNo, contract.Title,
                ContractDeliverableRules.ReminderMessage(item.Name, item.DeliveryDate!.Value, dState, today, calendar), item.DeliveryDate, dState == DeliverableState.Overdue ? "danger" : "warning"));
        }

        // 待我核決
        var pending = await contracts.SearchAsync(userId, new ContractSearchRequest
        {
            PageIndex = 1, PageSize = 1, Filter = new ContractSearchFilter { PendingMyDecision = true }
        }, ct).ConfigureAwait(false);

        var ordering = new Dictionary<string, int> { ["danger"] = 0, ["warning"] = 1 };
        return new ContractDashboardDto(
            mine.Count, current.Values.Sum(), pending.TotalCount, coopOverdue, coopNoticeOverdue, warrantyExpiring, invoiceableCount, invoiceableAmount, acceptanceSoon,
            alerts.OrderBy(a => ordering.GetValueOrDefault(a.Severity, 2)).ThenBy(a => a.Date).Take(MaxAlerts).ToList(), deliverablesOverdue);
    }
}
