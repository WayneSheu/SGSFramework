using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGSFramework.Alert.Abstractions;
using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Text;

namespace SGSFramework.Alert.Channels
{
    /// <summary>
    /// SMTP 電子郵件告警管道策略實作
    /// </summary>
    public sealed class SmtpNotificationChannel : IAlertNotificationChannel
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SmtpNotificationChannel> _logger;

        public string ChannelName => "SMTP_Email";

        public SmtpNotificationChannel(IConfiguration configuration, ILogger<SmtpNotificationChannel> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsConfigured()
        {
            string? host = _configuration["Smtp:Host"];
            string? toAddress = _configuration["Smtp:ToAddress"];
            return !string.IsNullOrWhiteSpace(host) && !string.IsNullOrWhiteSpace(toAddress);
        }

        public async Task<bool> SendAlertAsync(AlertMessageModel alert, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(alert);

            if (!IsConfigured())
            {
                _logger.LogWarning("[SMTP] 發送跳過：未配置有效的 Host 或 ToAddress。AlertId: {AlertId}", alert.AlertId);
                return false;
            }

            try
            {
                string host = _configuration["Smtp:Host"]!;
                int port = int.TryParse(_configuration["Smtp:Port"], out var p) ? p : 25;
                string fromAddress = _configuration["Smtp:FromAddress"] ?? "noreply@sgs.com";
                string toAddress = _configuration["Smtp:ToAddress"]!;

                using var client = new SmtpClient(host, port);
                using var mailMessage = new MailMessage(fromAddress, toAddress)
                {
                    Subject = $"[系統告警][{alert.AlertCode}] - {alert.AlertId}",
                    Body = $"告警來源模組: {alert.SourceModule}\n時間: {alert.CreatedAtUtc:yyyy-MM-dd HH:mm:ss zzz}\n內容: {alert.Message}\n例外細節: {alert.ExceptionDetails}",
                    IsBodyHtml = false
                };

                await client.SendMailAsync(mailMessage, cancellationToken);
                _logger.LogInformation("[SMTP] 告警郵件發送成功。AlertId: {AlertId}", alert.AlertId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SMTP] 告警郵件發送失敗。AlertId: {AlertId}", alert.AlertId);
                return false;
            }
        }
    }
}
