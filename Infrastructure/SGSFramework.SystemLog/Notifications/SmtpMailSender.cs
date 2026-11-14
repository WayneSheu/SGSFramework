#nullable enable
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.Core.Abstractions.Notifications;
using SGSFramework.SystemLog.Options;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.SystemLog.Notifications
{
    /// <summary>
    /// 一般郵件寄送實作（.NET SmtpClient，沿用 <c>AlertSettings:Smtp</c> 的伺服器與認證設定）。
    /// 與 <see cref="EmailNotificationStrategy"/> 不同：不寫告警標頭、不吞例外，供「寄給客戶」這類需要確認寄出結果的情境使用。
    /// </summary>
    public sealed class SmtpMailSender : IMailSender
    {
        private const string SenderDisplayName = "PhysLIMS 專案管理系統";
        private const int MaxRecipients = 20;

        private readonly IOptionsMonitor<AlertSettings> _settings;
        private readonly ILogger<SmtpMailSender> _logger;

        public SmtpMailSender(IOptionsMonitor<AlertSettings> settings, ILogger<SmtpMailSender> logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>媒體類型格式不正確時退回 application/octet-stream，避免 Attachment 建構失敗。</summary>
        private static string SafeMediaType(string? value)
        {
            try
            {
                return string.IsNullOrWhiteSpace(value) ? MediaTypeNames.Application.Octet : new ContentType(value).MediaType;
            }
            catch (FormatException)
            {
                return MediaTypeNames.Application.Octet;
            }
        }

        public async Task SendAsync(MailRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var config = _settings.CurrentValue.Smtp;
            if (string.IsNullOrWhiteSpace(config.Host) || string.IsNullOrWhiteSpace(config.FromAddress))
                throw new MailNotConfiguredException("系統尚未設定 SMTP 伺服器或寄件人地址（AlertSettings:Smtp）。");
            if (request.To.Count == 0) throw new ArgumentException("至少需要一位收件人。", nameof(request));
            if (request.To.Count + request.Cc.Count > MaxRecipients) throw new ArgumentException($"收件人與副本合計不得超過 {MaxRecipients} 位。", nameof(request));

            // 主旨不得含換行（防標頭注入）；收件地址由 MailAddress 驗證格式
            var subject = request.Subject.Replace("\r", " ").Replace("\n", " ").Trim();

            using var mail = new MailMessage
            {
                From = new MailAddress(config.FromAddress, SenderDisplayName, Encoding.UTF8),
                Subject = subject,
                SubjectEncoding = Encoding.UTF8,
                Body = request.HtmlBody,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true,
                Priority = MailPriority.Normal
            };

            foreach (var address in request.To) mail.To.Add(new MailAddress(address.Trim()));
            foreach (var address in request.Cc) mail.CC.Add(new MailAddress(address.Trim()));
            if (!string.IsNullOrWhiteSpace(request.ReplyTo)) mail.ReplyToList.Add(new MailAddress(request.ReplyTo.Trim()));

            // 附件以 MemoryStream 持有直到寄送完成（using 結束後才釋放）
            var streams = new System.Collections.Generic.List<MemoryStream>();
            try
            {
                foreach (var attachment in request.Attachments ?? [])
                {
                    var stream = new MemoryStream(attachment.Content, writable: false);
                    streams.Add(stream);
                    mail.Attachments.Add(new Attachment(stream, attachment.FileName, SafeMediaType(attachment.ContentType))
                    {
                        NameEncoding = Encoding.UTF8
                    });
                }

                using var client = new SmtpClient(config.Host, config.Port)
                {
                    EnableSsl = config.EnableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network
                };

                if (!string.IsNullOrWhiteSpace(config.Username) && !string.IsNullOrWhiteSpace(config.Password))
                {
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(config.Username, config.Password);
                }

                await Task.Run(() => client.Send(mail), cancellationToken).ConfigureAwait(false);

                // 紀錄收件人數量與主旨，不記錄地址與內容（個資）
                _logger.LogInformation("郵件已由 SMTP 伺服器接收並外發。收件 {ToCount} 位、副本 {CcCount} 位、附件 {AttachmentCount} 個。",
                    request.To.Count, request.Cc.Count, request.Attachments?.Count ?? 0);
            }
            finally
            {
                foreach (var stream in streams) stream.Dispose();
            }
        }
    }
}
