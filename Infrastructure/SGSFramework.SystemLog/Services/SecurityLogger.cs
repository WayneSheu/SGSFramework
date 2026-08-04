#nullable enable
using Serilog;
using Serilog.Context;
using SGSFramework.Core.Abstractions.Logings;
using System;
using System.Security.Cryptography;
using System.Text;

namespace SGSFramework.SystemLog.Services
{
    /// <summary>
    /// 全域防篡改資安審計日誌管理器，確保所有安全事件都能被追蹤與稽核
    /// </summary>
    public sealed class SecurityLogger : ISecurityLogger
    {
        private readonly Serilog.ILogger _securityLogger;

        public SecurityLogger()
        {
            // 初始化時指定 SourceContext，確保精準路由
            // 確保 SourceContext 統一，方便在 ELK/Splunk 中篩選
            _securityLogger = Log.ForContext("SourceContext", "SecurityEventSource");
        }


        // 優化後的關鍵點：引入 eventCode 作為關鍵索引，統一格式
        public void LogSecurityWarning(string eventCode, string messageTemplate, params object?[] propertyValues)
        {
            // 確保每一個安全警告都帶有 EventCode
            _securityLogger.Warning($"[EventCode:{eventCode}] {messageTemplate}", propertyValues);
        }

        public void LogSecurity(
            string eventCode,
            string eventCategory,
            string? userId,
            string clientIp,
            string messageTemplate,
            params object?[] propertyValues)
        {
            ArgumentNullException.ThrowIfNull(messageTemplate);
            ArgumentException.ThrowIfNullOrEmpty(eventCode);
            ArgumentException.ThrowIfNullOrEmpty(eventCategory);
            ArgumentException.ThrowIfNullOrEmpty(clientIp);

            try
            {
                // 1. 強型別防禦性處理
                string safeUserId = userId ?? "ANONYMOUS";
                string decoratedTemplate = $"[{eventCode}] {messageTemplate}";

                // 2. 擷取或生成當前請求的 CorrelationId (精準對齊資料庫欄位)
                string correlationId = System.Diagnostics.Activity.Current?.RootId
                                       ?? System.Diagnostics.Activity.Current?.Id
                                       ?? Guid.NewGuid().ToString("D");

                // 3. 預判日誌層級，若符合過濾規則 (Warning 以上) 則提前生成 AlertId 確保雙軌對齊
                bool isAlertable = eventCode.StartsWith("SEC-911") ||// 明確加入高危事件碼
                                   eventCode.Equals("SEC-MOD-001") || // 明確加入模組載入錯誤
                                   eventCode.Contains("MALFORMED") ||// 明確加入惡意請求事件碼
                                   eventCode.Contains("ATTACK") ||// 明確加入攻擊事件碼
                                   eventCode.Contains("401") ||// 明確加入未授權事件碼
                                   eventCode.Contains("403") ||// 明確加入禁止事件碼
                                   eventCode.Contains("INVALID") ||// 明確加入無效事件碼
                                   eventCode.Contains("REPLAY");// 明確加入重放攻擊事件碼

                string? alertId = isAlertable ? Guid.NewGuid().ToString("n") : null;

                // 4. 計算確定性安全防禦特徵雜湊 (Fingerprint)
                string fingerprint = ComputeSecurityFingerprint(eventCode, eventCategory, safeUserId, clientIp, messageTemplate);

                // 5. 將所有一等公民屬性壓入日誌 Context 字典中
                var scopedLogger = _securityLogger
                    .ForContext("LogType", "Security")
                    .ForContext("EventCategory", eventCategory)
                    .ForContext("UserId", safeUserId)
                    .ForContext("ClientIp", clientIp)
                    .ForContext("CorrelationId", correlationId)
                    .ForContext("AlertId", alertId)
                    .ForContext("Fingerprint", fingerprint);

                // 6. 依據事件嚴重性分級輸出
                if (eventCode.StartsWith("SEC-911") || eventCode.Contains("SEC-MOD-001")  || eventCode.Contains("MALFORMED") || eventCode.Contains("ATTACK"))
                {
                    scopedLogger.Fatal(decoratedTemplate, propertyValues);
                }
                else if (eventCode.Contains("401") || eventCode.Contains("403") || eventCode.Contains("INVALID") || eventCode.Contains("REPLAY"))
                {
                    // 臨時增加：觀察控制台是否有輸出，確認 Sink 沒漏掉
                    Console.WriteLine($"[DEBUG] 嘗試寫入稽核事件: {decoratedTemplate}");
                    scopedLogger.Warning(decoratedTemplate, propertyValues);
                }
                else
                {
                    scopedLogger.Information(decoratedTemplate, propertyValues);
                }
            }
            catch (Exception ex)
            {
                Serilog.Debugging.SelfLog.WriteLine($"[AuditLogger Critical Error] Failed to log security event: {ex.Message}");
            }
        }

        /// <summary>
        /// 運用密碼學雜湊計算確定性(Deterministic)防禦特徵，提供資安表追蹤依據
        /// </summary>
        private static string ComputeSecurityFingerprint(string eventCode, string category, string userId, string ip, string template)
        {
            string rawInput = $"{eventCode}|{category}|{userId}|{ip}|{template}";
            byte[] inputBytes = Encoding.UTF8.GetBytes(rawInput);
            byte[] hashBytes = SHA256.HashData(inputBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}