namespace SGSFramework.Alert.Channels
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using SGSFramework.Alert.Abstractions;

    public sealed class LoggingFallbackNotificationChannel : IAlertNotificationChannel
    {
        private readonly ILogger<LoggingFallbackNotificationChannel> _logger;

        public string ChannelName => "System_Log_Fallback";

        public LoggingFallbackNotificationChannel(ILogger<LoggingFallbackNotificationChannel> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsConfigured() => true;

        public Task<bool> SendAlertAsync(AlertMessageModel alert, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(alert);

            // 已將 alert.EventType 改為 alert.AlertCode，alert.Source 改為 alert.SourceModule
            _logger.LogCritical("[FALLBACK_ALERT] AlertId: {AlertId} | Code: {AlertCode} | Source: {SourceModule} | Msg: {Message}",
                alert.AlertId, alert.AlertCode, alert.SourceModule, alert.Message);

            return Task.FromResult(true);
        }
    }
}
