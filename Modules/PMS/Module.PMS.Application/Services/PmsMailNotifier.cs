using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Notifications;

namespace SGS.Modules.PMS.Application.Services;

/// <summary>
/// PMS 業務提醒信（LogType=Business，沿用系統告警 SMTP 與中性版型）：解析專案成員 Email 並寄送。
/// 寄送失敗只記錄、不拋出（提醒信不應讓主要作業失敗）；系統未啟用告警通知服務時回傳 false。
/// </summary>
public sealed class PmsMailNotifier(
    UserManager<ApplicationUser> userManager,
    ILogger<PmsMailNotifier> logger,
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
    public async Task<bool> SendAsync(
        string subject, string htmlBody, IReadOnlyList<string> recipients, string ruleName, string fingerprint, DateTimeOffset now, CancellationToken ct)
    {
        if (alertCoordinator is null || recipients.Count == 0) return false;

        var message = new AlertMessage
        {
            LogType = "Business",
            LogLevel = "Warning",
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
            logger.LogError(ex, "[PMS] 業務提醒信寄送失敗：{Subject}", subject);
            return false;
        }
    }
}
