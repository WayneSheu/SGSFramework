using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Notifications;

namespace SGS.Modules.QTN.Application.Services;

/// <summary>
/// QTN 業務提醒信（LogType=Business，沿用系統告警 SMTP）：送審、核准、退件、報價到期、CR 評估逾期、復盤到期。
/// 寄送失敗只記錄、不拋出；系統未啟用告警通知服務時略過。信件內容不含成本與毛利率。
/// </summary>
public sealed class QtnMailNotifier(
    UserManager<ApplicationUser> userManager,
    TimeProvider clock,
    ILogger<QtnMailNotifier> logger,
    IAlertCoordinator? alertCoordinator = null)
{
    public bool IsAvailable => alertCoordinator is not null;

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

    /// <summary>寄送提醒信。<paramref name="lines"/> 為純文字，於此統一 HTML 編碼。</summary>
    public async Task<bool> NotifyAsync(IEnumerable<Guid> userIds, string subject, IEnumerable<string> lines, string ruleName, string fingerprint, CancellationToken ct)
    {
        if (alertCoordinator is null) return false;

        var recipients = await ResolveEmailsAsync(userIds, ct).ConfigureAwait(false);
        if (recipients.Count == 0) return false;

        var body = "<p>" + string.Join("<br/>", lines.Select(WebUtility.HtmlEncode)) + "</p><p style=\"color:#666\">本信由報價管理系統自動寄送。</p>";
        var message = new AlertMessage
        {
            LogType = "Business",
            LogLevel = "Information",
            MessageTemplate = subject,
            RenderedMessage = body,
            Fingerprint = fingerprint,
            RuleName = ruleName,
            Recipients = string.Join(',', recipients),
            Timestamp = clock.GetUtcNow()
        };

        try
        {
            await alertCoordinator.DispatchAsync(message, ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[QTN] 業務提醒信寄送失敗：{Subject}", subject);
            return false;
        }
    }
}
