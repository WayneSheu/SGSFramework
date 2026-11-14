using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.PMS.Application.Contracts;
using SGS.Modules.PMS.Application.Rules;
using SGS.Modules.PMS.Infrastructure.Dbcontexts;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Notifications;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// CR 評估逾期提醒（pms-cr-eval-watch）：掃描逾期仍未評估的 CR，依專案彙整成一封信寄給該專案的 Tech Lead 與 PM。
/// 同一張 CR 每個台灣日最多提醒一次；非工作日不發；找不到收件人 Email 的專案不標記已提醒（下次再試）。
/// </summary>
public sealed class ChangeRequestWatchService(
    PMSDbContext db,
    HolidayService holidays,
    UserManager<ApplicationUser> userManager,
    TimeProvider clock,
    ILogger<ChangeRequestWatchService> logger,
    IAlertCoordinator? alertCoordinator = null)
{
    public async Task<CrEvalWatchResult> ScanOverdueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var calendar = await holidays.LoadCalendarAsync(now, ct).ConfigureAwait(false);
        if (!CrEvalWatch.IsWorkdayToday(now, calendar))
            return new CrEvalWatchResult(0, 0, 0, 0, "今天不是工作日，略過評估逾期提醒。");

        // 先以 DB 粗篩（受理待評估且已逾期），再以純函式判斷「今天是否已提醒過」
        var candidates = await db.ChangeRequests
            .Where(c => c.Status == CrStatus.Received && c.EvalDueAt < now)
            .Include(c => c.Project).ThenInclude(p => p!.Members)
            .ToListAsync(ct).ConfigureAwait(false);

        var due = candidates.Where(c => CrEvalWatch.ShouldRemind(c.Status, c.EvalDueAt, c.LastEvalReminderAt, now)).ToList();
        if (due.Count == 0)
            return new CrEvalWatchResult(candidates.Count, 0, 0, 0, candidates.Count == 0 ? "沒有逾期未評估的 CR。" : "逾期的 CR 今天都已提醒過。");

        if (alertCoordinator is null)
        {
            logger.LogWarning("[PMS] 找到 {Count} 張評估逾期的 CR，但系統未啟用告警通知服務，無法寄信。", due.Count);
            return new CrEvalWatchResult(due.Count, 0, 0, due.Count, "系統未啟用告警通知服務，無法寄出提醒。");
        }

        var notifiedCrs = 0;
        var notifiedProjects = 0;
        var skipped = 0;

        foreach (var group in due.GroupBy(c => c.ProjectId))
        {
            var project = group.First().Project!;
            var recipientIds = project.Members
                .Where(m => m.RaciRole is RaciRole.TechLead or RaciRole.Pm)
                .Select(m => m.UserId).Distinct().ToList();
            var emails = await ResolveEmailsAsync(recipientIds, ct).ConfigureAwait(false);

            var items = group.Select(c => new CrEvalOverdueItem(c.CrNo, c.Title, c.Kind, c.EvalDueAt, c.ReceivedAt)).ToList();

            if (emails.Count == 0)
            {
                skipped += group.Count();
                logger.LogWarning("[PMS] 專案 {ProjectCode} 有 {Count} 張 CR 評估逾期，但找不到 Tech Lead／PM 的 Email，未寄出提醒。",
                    project.Code, items.Count);
                continue;
            }

            var message = new AlertMessage
            {
                LogType = "Business",
                LogLevel = "Warning",
                MessageTemplate = CrEvalWatchMessage.Subject(project.Code, items.Count),
                RenderedMessage = CrEvalWatchMessage.Body(project.Code, project.Name, items, now),
                Fingerprint = $"pms.cr.eval-overdue:{project.Id:N}:{now:yyyyMMdd}",
                RuleName = "PMS CR 評估逾期提醒",
                Recipients = string.Join(',', emails),
                Timestamp = now
            };

            try
            {
                await alertCoordinator.DispatchAsync(message, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                skipped += group.Count();
                logger.LogError(ex, "[PMS] 專案 {ProjectCode} 的 CR 評估逾期提醒寄送失敗。", project.Code);
                continue;
            }

            foreach (var cr in group) cr.LastEvalReminderAt = now;
            notifiedCrs += group.Count();
            notifiedProjects++;
        }

        if (notifiedCrs > 0) await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var summary = $"逾期 {due.Count} 張，已提醒 {notifiedCrs} 張（{notifiedProjects} 個專案）"
                      + (skipped > 0 ? $"；{skipped} 張因無收件人或寄送失敗未提醒，下次再試" : null);
        logger.LogInformation("[PMS] CR 評估逾期掃描：{Summary}", summary);
        return new CrEvalWatchResult(due.Count, notifiedCrs, notifiedProjects, skipped, summary);
    }

    private async Task<List<string>> ResolveEmailsAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var emails = new List<string>();
        foreach (var id in userIds)
        {
            ct.ThrowIfCancellationRequested();
            var user = await userManager.FindByIdAsync(id.ToString()).ConfigureAwait(false);
            if (user is null || !user.CanLogin() || string.IsNullOrWhiteSpace(user.Email)) continue;
            emails.Add(user.Email.Trim());
        }

        return emails.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
