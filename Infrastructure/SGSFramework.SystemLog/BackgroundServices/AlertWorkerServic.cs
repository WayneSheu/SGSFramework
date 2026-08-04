#nullable enable
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Events;
using SGSFramework.Core.Abstractions.Notifications;
using SGSFramework.SystemLog.Options;
using SGSFramework.SystemLog.Services;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace SGSFramework.SystemLog.BackgroundServices
{
    /// <summary>
    /// 系統告警非同步背景過濾服務 (第一級：日誌排空與防洪抑制)
    /// </summary>
    public sealed class AlertWorkerService : BackgroundService
    {
        private readonly AlertMemoryChannel _alertChannel;
        private readonly IAlertCoordinator _coordinator;
        private readonly IOptionsMonitor<AlertSettings> _settings;
        private readonly ILogger<AlertWorkerService> _logger;
        private readonly ConcurrentDictionary<string, DateTime> _floodBarrierCache = new();

        public AlertWorkerService(
            AlertMemoryChannel alertChannel,
            IAlertCoordinator coordinator,
            IOptionsMonitor<AlertSettings> settings,
            ILogger<AlertWorkerService> logger)
        {
            _alertChannel = alertChannel ?? throw new ArgumentNullException(nameof(alertChannel));
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("AlertWorkerService 啟動，開始監聽日誌告警佇列...");

            while (!_alertChannel.Reader.Completion.IsCompleted && !stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (await _alertChannel.Reader.WaitToReadAsync(stoppingToken).ConfigureAwait(false))
                    {
                        while (_alertChannel.Reader.TryRead(out var logEvent))
                        {
                            if (!_settings.CurrentValue.IsEnabled) continue;

                            // 💡 修正：對齊全域識別碼映射
                            var alertMessage = MapToAlertMessage(logEvent);

                            if (IsFlooding(alertMessage.Fingerprint))
                            {
                                _logger.LogDebug("觸發防洪機制，抑制重複告警分發。指紋: {Fingerprint}", alertMessage.Fingerprint);
                                continue;
                            }

                            await _coordinator.DispatchAsync(alertMessage, stoppingToken).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "告警異步背景處理服務發生核心排空異常");
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
                }
            }
        }

        private bool IsFlooding(string fingerprint)
        {
            var now = DateTime.UtcNow;
            var cooldown = TimeSpan.FromSeconds(_settings.CurrentValue.CooldownSeconds);

            if (_floodBarrierCache.TryGetValue(fingerprint, out var nextAllowedTime) && now < nextAllowedTime)
            {
                return true;
            }

            _floodBarrierCache[fingerprint] = now.Add(cooldown);

            if (_floodBarrierCache.Count > 1000)
            {
                CleanExpiredCache();
            }

            return false;
        }

        private void CleanExpiredCache()
        {
            var now = DateTime.UtcNow;
            foreach (var (key, value) in _floodBarrierCache)
            {
                if (now >= value) _floodBarrierCache.TryRemove(key, out _);
            }
        }

        private static AlertMessage MapToAlertMessage(LogEvent logEvent)
        {
            // 安全屬性解包輔助工具 (防範帶有雙引號)
            string? GetScalarProp(string name)
            {
                if (logEvent.Properties.TryGetValue(name, out var val) && val is ScalarValue scalar)
                {
                    return scalar.Value?.ToString()?.Trim('"');
                }
                return null;
            }

            var isSecurity = "Security".Equals(GetScalarProp("LogType"), StringComparison.OrdinalIgnoreCase);
            var renderedMsg = logEvent.RenderMessage();
            var template = logEvent.MessageTemplate.Text;

            // 💡 核心修正：精準提取上游掛載的唯一 ID，杜絕通知端識別碼不符的病因
            var alertId = GetScalarProp("AlertId") ?? Guid.NewGuid().ToString("n");
            var correlationId = GetScalarProp("CorrelationId");

            var exceptionTypeName = logEvent.Exception?.GetType().FullName ?? string.Empty;
            var rawFingerprint = $"{template}_{exceptionTypeName}_{logEvent.Level}";
            var fingerprintHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawFingerprint))).ToLowerInvariant();

            return new AlertMessage
            {
                AlertId = alertId, // 確保與資料庫 100% 同步
                CorrelationId = correlationId,
                LogType = isSecurity ? "Security" : "System",
                LogLevel = logEvent.Level.ToString(),
                MessageTemplate = template,
                RenderedMessage = renderedMsg,
                Timestamp = logEvent.Timestamp,
                ExceptionDetails = logEvent.Exception?.ToString(),
                Fingerprint = fingerprintHash
            };
        }
    }
}