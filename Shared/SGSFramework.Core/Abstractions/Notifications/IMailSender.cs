namespace SGSFramework.Core.Abstractions.Notifications
{
    /// <summary>郵件附件（內容全在記憶體，適用於 PDF 報告等小型檔案）。</summary>
    public sealed record MailAttachment(string FileName, string ContentType, byte[] Content);

    /// <summary>
    /// 對外郵件請求（寄給客戶等一般收件人）。與系統告警信（<see cref="IAlertCoordinator"/>）不同：使用中性版型，不帶告警標示。
    /// </summary>
    /// <param name="To">收件人（至少一位）。</param>
    /// <param name="Cc">副本（可空）。</param>
    /// <param name="Subject">主旨（不得含換行）。</param>
    /// <param name="HtmlBody">HTML 內容；呼叫端須自行對動態文字做 HTML 編碼。</param>
    /// <param name="ReplyTo">回覆地址（例如寄件的 PM），空值時使用寄件人。</param>
    public sealed record MailRequest(
        IReadOnlyList<string> To,
        IReadOnlyList<string> Cc,
        string Subject,
        string HtmlBody,
        IReadOnlyList<MailAttachment>? Attachments = null,
        string? ReplyTo = null);

    /// <summary>SMTP 未設定或郵件功能停用。</summary>
    public sealed class MailNotConfiguredException(string message) : InvalidOperationException(message);

    /// <summary>
    /// 一般郵件寄送服務（沿用系統 SMTP 設定）。寄送失敗一律拋出例外（與告警策略不同，呼叫端需要知道是否寄出）。
    /// </summary>
    public interface IMailSender
    {
        /// <summary>寄出郵件；SMTP 未設定拋出 <see cref="MailNotConfiguredException"/>，伺服器拒絕或連線失敗拋出對應例外。</summary>
        Task SendAsync(MailRequest request, CancellationToken cancellationToken);
    }
}
