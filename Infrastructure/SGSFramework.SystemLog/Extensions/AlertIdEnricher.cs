using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.Extensions
{
    /// <summary>
    /// 企業級告警識別碼自動配置增強器
    /// </summary>
    public sealed class AlertIdEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            ArgumentNullException.ThrowIfNull(logEvent);
            ArgumentNullException.ThrowIfNull(propertyFactory);

            // 1. 如果已由 AuditLogger 提前掛載了精準的 AlertId，則沿用，不進行覆蓋
            if (logEvent.Properties.ContainsKey("AlertId")) return;

            // 2. 識別是否為資安日誌
            bool isSecurity = logEvent.Properties.TryGetValue("LogType", out var logType) &&
                             logType is ScalarValue sv && "Security".Equals(sv.Value?.ToString(), StringComparison.OrdinalIgnoreCase);

            // 3. 對齊分流邏輯：資安 >= Warning, 系統 >= Error 則判定為應告警事件
            bool shouldAlert = isSecurity
                ? logEvent.Level >= LogEventLevel.Warning
                : logEvent.Level >= LogEventLevel.Error;

            if (shouldAlert)
            {
                // 統一生成 32 位元不帶連字號的乾淨 GUID (與資料庫 CHAR(32) 對齊)
                var cleanAlertId = Guid.NewGuid().ToString("n");
                logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("AlertId", cleanAlertId));
            }
        }
    }
}
