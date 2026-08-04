// Path: Infrastructure/SGSFramework.SystemLog/Notifications/EmailNotificationStrategy.cs
#nullable enable
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.Core.Abstractions.Notifications;
using SGSFramework.SystemLog.Options;
using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.SystemLog.Notifications
{
    /// <summary>
    /// 系統告警郵件發送策略實作（採用 .NET 標準 SmtpClient 管線）
    /// </summary>
    public sealed class EmailNotificationStrategy : INotificationStrategy
    {
        private readonly IOptionsMonitor<AlertSettings> _settings;
        private readonly ILogger<EmailNotificationStrategy> _logger;

        public string ChannelName => "SMTP_Email";

        public EmailNotificationStrategy(
            IOptionsMonitor<AlertSettings> settings,
            ILogger<EmailNotificationStrategy> logger)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SendAlertAsync(AlertMessage message, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message); // C# 10+ 強型別安全檢查

            var config = _settings.CurrentValue.Smtp;

            if (string.IsNullOrEmpty(config.Host) || string.IsNullOrEmpty(config.ToAddresSGSFramework))
            {
                _logger.LogWarning("SMTP 發送失敗：未配置有效的 Host 或 ToAddresSGSFramework。");
                return;
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

            var isSecurity = "Security".Equals(message.LogType, StringComparison.OrdinalIgnoreCase);
            var subjectPrefix = isSecurity ? "🚨 [資安威脅警報]" : "🔥 [系統崩潰異常]";

            var rawSummary = message.RenderedMessage ?? string.Empty;
            var cleanedSummary = Regex.Replace(rawSummary, @"\s+", " ").Trim();

            if (cleanedSummary.Length > 60)
            {
                cleanedSummary = $"{cleanedSummary[..60]}...";
            }

            using var mailMessage = new MailMessage
            {
                From = new MailAddress(config.FromAddress ?? "SGSFramework-alert@yourdomain.com", "SGSFramework 智慧賦能監控中樞"),
                Subject = $"{subjectPrefix} ({message.LogLevel}) - {cleanedSummary}",
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true
            };

            // =========================================================================
            // 💡 核心優化：加註「重要郵件」標頭管線（觸發 Outlook 紅色驚嘆號與 Gmail 重要標籤）
            // =========================================================================
            if (isSecurity || "Critical".Equals(message.LogLevel, StringComparison.OrdinalIgnoreCase) || "Error".Equals(message.LogLevel, StringComparison.OrdinalIgnoreCase))
            {
                // 1. 標準生產級屬性設為高優先權
                mailMessage.Priority = MailPriority.High;

                // 2. 補齊 RFC 標準與各大郵件伺服器（Outlook / Yahoo / Apple Mail）識別的強烈重要性標頭
                mailMessage.Headers.Add("Importance", "High");
                mailMessage.Headers.Add("X-Priority", "1"); // 1 = Highest, 3 = Normal, 5 = Lowest
                mailMessage.Headers.Add("X-MSMail-Priority", "High");
            }
            else
            {
                mailMessage.Priority = MailPriority.Normal;
                mailMessage.Headers.Add("Importance", "Normal");
                mailMessage.Headers.Add("X-Priority", "3");
            }

            var statusColor = isSecurity ? "#E63946" : "#D90429";
            var headerText = isSecurity ? "SECURITY AUDIT ALERT" : "SYSTEM CRITICAL ALERT";

            mailMessage.Body = $"""
            <div style="font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif; background-color: #F8F9FA; padding: 24px; color: #212529;">
                <div style="max-width: 600px; margin: 0 auto; background: #FFFFFF; border-radius: 8px; border-top: 4px solid {statusColor}; box-shadow: 0 4px 6px rgba(0,0,0,0.05); padding: 24px;">
                    <h2 style="color: {statusColor}; margin-top: 0; font-size: 20px; border-bottom: 1px solid #E9ECEF; padding-bottom: 12px; letter-spacing: 0.5px;">
                        {headerText}
                    </h2>
                    <table style="width: 100%; font-size: 14px; border-collapse: collapse; margin-top: 16px;">
                        <tr>
                            <td style="padding: 6px 0; color: #6C757D; width: 100px; font-weight: bold;">日誌類型:</td>
                            <td style="padding: 6px 0; font-weight: bold; color: {statusColor};">{message.LogType}</td>
                        </tr>
                        <tr>
                            <td style="padding: 6px 0; color: #6C757D; font-weight: bold;">告警級別:</td>
                            <td style="padding: 6px 0;"><span style="background: {statusColor}; color: #FFF; padding: 2px 8px; border-radius: 4px; font-size: 12px; font-weight: bold;">{message.LogLevel}</span></td>
                        </tr>
                        <tr>
                            <td style="padding: 6px 0; color: #6C757D; font-weight: bold;">觸發時間:</td>
                            <td style="padding: 6px 0; font-family: monospace;">{message.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}</td>
                        </tr>
                        {(string.IsNullOrEmpty(message.CorrelationId) ? "" : $"""
                        <tr>
                            <td style="padding: 6px 0; color: #6C757D; font-weight: bold;">追蹤代碼:</td>
                            <td style="padding: 6px 0; font-family: monospace; color: #0D6EFD;">{message.CorrelationId}</td>
                        </tr>
                        """)}
                    </table>
                    
                    <div style="margin-top: 20px; background: #F8F9FA; border-left: 4px solid #6C757D; padding: 12px 16px;">
                        <strong style="font-size: 14px; color: #495057;">事件摘要：</strong>
                        <p style="margin: 6px 0 0 0; font-size: 14px; line-height: 1.6; color: #212529; font-family: 'Consolas', monospace;">{message.RenderedMessage}</p>
                    </div>

                    {(string.IsNullOrEmpty(message.ExceptionDetails) ? "" : $"""
                    <div style="margin-top: 20px;">
                        <strong style="font-size: 14px; color: #DC3545;">異常堆疊追蹤 (Stack Trace)：</strong>
                        <pre style="color: #C92A2A; background: #FFF5F5; border: 1px solid #FFC9C9; padding: 16px; border-radius: 4px; font-family: 'Consolas', 'Monaco', monospace; font-size: 13px; overflow-x: auto; white-space: pre-wrap; word-break: break-all; margin-top: 8px;">{message.ExceptionDetails}</pre>
                    </div>
                    """)}
                    
                    <hr style="border: 0; border-top: 1px solid #E9ECEF; margin: 24px 0;" />
                    <p style="font-size: 12px; color: #6C757D; margin-bottom: 0; line-height: 1.5;">
                        告警唯一追蹤碼 (Alert ID): <span style="font-family: monospace; color: #495057; font-weight: bold;">{message.AlertId}</span><br/>
                        安全防禦特徵雜湊 (Fingerprint): <span style="font-family: monospace; color: #6C757D;">{message.Fingerprint}</span>
                    </p>
                </div>
            </div>
            """;

            foreach (var address in config.ToAddresSGSFramework.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                mailMessage.To.Add(address.Trim());
            }

            try
            {
                if (cancellationToken.IsCancellationRequested) return;

                await Task.Run(() => client.Send(mailMessage), cancellationToken);

                _logger.LogInformation("告警郵件已成功由 SMTP 伺服器接收並外發！AlertId: {AlertId}, 通道: {Channel}", message.AlertId, ChannelName);
            }
            catch (Exception ex)
            {
                Serilog.Debugging.SelfLog.WriteLine($"[SMTP Fatal] Email發送慘遭郵件伺服器拒絕拒連。AlertId: {message.AlertId}, 原因: {ex.Message}");
                throw;
            }
        }
    }
}