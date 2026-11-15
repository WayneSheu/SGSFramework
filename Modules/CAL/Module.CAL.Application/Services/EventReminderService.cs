using System.Net;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.CAL.Application.Contracts;
using SGS.Modules.CAL.Application.Rules;
using SGS.Modules.CAL.Infrastructure.Dbcontexts;
using SGS.Modules.CAL.Infrastructure.Entities;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Notifications;

namespace SGS.Modules.CAL.Application.Services;

/// <summary>
/// CAL 提醒信（LogType=Business，沿用系統告警 SMTP 與中性版型）：解析收件人 Email 並寄送。
/// 寄送失敗只記錄、不拋出（下次排程再試）；系統未啟用告警通知服務時 <see cref="IsAvailable"/> 為 false。
/// </summary>
public sealed class CalMailNotifier(
    UserManager<ApplicationUser> userManager,
    ILogger<CalMailNotifier> logger,
    IAlertCoordinator? alertCoordinator = null)
{
    public bool IsAvailable => alertCoordinator is not null;

    /// <summary>以使用者識別碼取得可登入且有 Email 的收件人（去重、忽略大小寫）。</summary>
    public async Task<IReadOnlyList<string>> ResolveEmailsAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var emails = new List<string>();
        foreach (var id in userIds.Distinct())
        {
            ct.ThrowIfCancellationRequested();
            var user = await userManager.FindByIdAsync(id.ToString()).ConfigureAwait(false);
            if (user is null || !user.CanLogin() || string.IsNullOrWhiteSpace(user.Email)) continue;
            emails.Add(user.Email.Trim());
        }

        return emails.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <param name="htmlBody">信件內容（HTML；呼叫端須自行對動態文字做 HTML 編碼）。</param>
    public async Task<bool> SendAsync(string subject, string htmlBody, IReadOnlyList<string> recipients, string ruleName, string fingerprint, DateTimeOffset now, CancellationToken ct)
    {
        if (alertCoordinator is null || recipients.Count == 0) return false;

        var message = new AlertMessage
        {
            LogType = "Business",
            LogLevel = "Information",
            MessageTemplate = subject,
            RenderedMessage = htmlBody,
            Fingerprint = fingerprint,
            RuleName = ruleName,
            Recipients = string.Join(',', recipients),
            Timestamp = now
        };

        try
        {
            await alertCoordinator.DispatchAsync(message, ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[CAL] 提醒信寄送失敗：{Subject}", subject);
            return false;
        }
    }
}

/// <summary>
/// 活動提醒派送（SCH 每 5 分鐘呼叫）：找出「已到提醒時間、尚未寄出、活動尚未開始」的提醒，寄 Email 給活動建立者與與會者。
/// 每組提醒只寄一次（成功才記錄寄出時間，失敗下次再試，直到活動開始）；活動改期會重設提醒。找不到任何可寄的 Email 時也標記為已處理，避免每 5 分鐘重試。
/// </summary>
public sealed class EventReminderService(CALDbContext db, CalMailNotifier notifier, TimeProvider clock, Microsoft.Extensions.Logging.ILogger<EventReminderService> logger)
{
    /// <summary>最多往後看多久的活動（對應提醒上限 14 天）。</summary>
    private static readonly TimeSpan Horizon = TimeSpan.FromMinutes(EventReminderRules.MaxMinutes);

    public async Task<ReminderDispatchResultDto> DispatchAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        if (!notifier.IsAvailable) return new ReminderDispatchResultDto(0, 0, 0, "系統未啟用告警通知服務，未寄送活動提醒。");

        var candidates = await db.CalendarEvents.Include(e => e.Reminders).Include(e => e.Attendees)
            .Where(e => e.StartAt > now && e.StartAt <= now + Horizon && e.Reminders.Any(r => r.SentAtUtc == null))
            .AsSplitQuery().ToListAsync(ct).ConfigureAwait(false);

        int sent = 0, noRecipients = 0, failed = 0;
        foreach (var ev in candidates)
        {
            ct.ThrowIfCancellationRequested();

            var due = ev.Reminders.Where(r => EventReminderRules.ShouldSend(ev.StartAt, r.MinutesBefore, r.SentAtUtc, now)).OrderBy(r => r.MinutesBefore).ToList();
            if (due.Count == 0) continue;

            // 同一活動同一輪到期多組提醒時只寄最近的一封（例如排程停擺後同時過期的「1 天前」與「1 小時前」），其餘一併標記
            var latest = due[0];
            var userIds = ev.Attendees.Select(a => a.UserId).Concat(ev.CreatedByUserId is { } creator ? [creator] : []).ToList();
            var recipients = await notifier.ResolveEmailsAsync(userIds, ct).ConfigureAwait(false);
            if (recipients.Count == 0)
            {
                foreach (var r in due) r.SentAtUtc = now;
                noRecipients++;
                logger.LogWarning("[CAL] 活動「{Title}」的提醒找不到任何有 Email 的收件人，已標記處理。", ev.Title);
                continue;
            }

            var ok = await notifier.SendAsync(
                Subject(ev, latest.MinutesBefore), Body(ev, latest.MinutesBefore), recipients, "CAL 活動提醒", $"cal.reminder:{latest.Id:N}", now, ct).ConfigureAwait(false);
            if (ok)
            {
                foreach (var r in due) r.SentAtUtc = now;
                sent++;
            }
            else
            {
                failed++;
            }
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var message = $"活動提醒：寄出 {sent}、無收件人 {noRecipients}、失敗 {failed}（下次再試）";
        logger.LogInformation("[CAL] {Message}", message);
        return new ReminderDispatchResultDto(sent, noRecipients, failed, message);
    }

    private static string When(CalendarEvent e)
    {
        var start = e.StartAt.ToOffset(TimeSpan.FromHours(8));
        var end = e.EndAt.ToOffset(TimeSpan.FromHours(8));
        return e.AllDay
            ? $"{start:yyyy-MM-dd}（全天）"
            : $"{start:yyyy-MM-dd HH:mm}～{(end.Date == start.Date ? end.ToString("HH:mm") : end.ToString("yyyy-MM-dd HH:mm"))}（台灣時間）";
    }

    public static string Subject(CalendarEvent e, int minutesBefore) => $"[行事曆提醒] {e.Title}（{EventReminderRules.Describe(minutesBefore).Replace("前", string.Empty)}後開始）";

    public static string Body(CalendarEvent e, int minutesBefore)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family: 'Microsoft JhengHei', Arial, sans-serif; font-size: 14px; line-height: 1.7; color: #212529;\">");
        sb.Append("<p>您好，下列行事曆活動將於 <strong>").Append(E(EventReminderRules.Describe(minutesBefore).Replace("前", string.Empty))).Append("後</strong>開始：</p>");
        sb.Append("<table style=\"border-collapse: collapse;\">");
        sb.Append("<tr><td style=\"padding: 2px 12px 2px 0; color: #6c757d;\">活動</td><td><strong>").Append(E(e.Title)).Append("</strong></td></tr>");
        sb.Append("<tr><td style=\"padding: 2px 12px 2px 0; color: #6c757d;\">時間</td><td>").Append(E(When(e))).Append("</td></tr>");
        if (!string.IsNullOrWhiteSpace(e.Location))
            sb.Append("<tr><td style=\"padding: 2px 12px 2px 0; color: #6c757d;\">地點</td><td>").Append(E(e.Location)).Append("</td></tr>");
        if (!string.IsNullOrWhiteSpace(e.Category))
            sb.Append("<tr><td style=\"padding: 2px 12px 2px 0; color: #6c757d;\">分類</td><td>").Append(E(e.Category)).Append("</td></tr>");
        sb.Append("</table>");
        if (!string.IsNullOrWhiteSpace(e.Description))
            sb.Append("<p style=\"margin-top: 12px;\">").Append(E(e.Description).Replace("\n", "<br />")).Append("</p>");
        sb.Append("<p style=\"color: #6c757d; font-size: 12px;\">此為系統自動寄出的行事曆提醒，請勿直接回覆。</p></div>");
        return sb.ToString();
    }
}
