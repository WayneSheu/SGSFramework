#nullable enable
using Serilog.Events;
using SGSFramework.Core.Abstractions.Entities.Alerts;

namespace SGSFramework.SystemLog.Alerts
{
    /// <summary>比對告警規則所需的日誌事實（與 Serilog 解耦，方便「規則測試」重用同一套比對邏輯）</summary>
    public sealed record AlertLogFacts(
        bool IsSecurity,
        LogEventLevel Level,
        string Message,
        string? Module,
        string? Operation,
        string? ExceptionType);

    /// <summary>某一時點的啟用規則快照（不可變，以整個替換方式熱更新）</summary>
    public sealed class AlertRuleSnapshot
    {
        public AlertRuleSnapshot(IReadOnlyList<AlertRule> rules)
        {
            Rules = rules;

            // 預先過濾用：只有「Alert」規則會讓事件進入告警佇列；Suppress 只在已進入佇列的事件上生效
            MinSystem = LowestLevel(rules, AlertRuleLogTypes.System);
            MinSecurity = LowestLevel(rules, AlertRuleLogTypes.Security);
        }

        /// <summary>依優先序排序的啟用規則</summary>
        public IReadOnlyList<AlertRule> Rules { get; }

        public LogEventLevel? MinSystem { get; }
        public LogEventLevel? MinSecurity { get; }

        private static LogEventLevel? LowestLevel(IEnumerable<AlertRule> rules, string logType)
        {
            LogEventLevel? lowest = null;
            foreach (var rule in rules)
            {
                if (rule.Action != AlertRuleActions.Alert) continue;
                if (rule.LogType != AlertRuleLogTypes.Any && rule.LogType != logType) continue;
                if (!AlertRuleEngine.TryParseLevel(rule.MinLevel, out var level)) continue;
                if (lowest is null || level < lowest) lowest = level;
            }
            return lowest;
        }
    }

    /// <summary>內建預設規則與目前生效快照的靜態持有者（Serilog 設定階段尚無 DI，故以靜態存取）</summary>
    public static class AlertRuleStore
    {
        private static volatile AlertRuleSnapshot _current = new(DefaultRules());

        public static AlertRuleSnapshot Current => _current;

        public static void Replace(AlertRuleSnapshot snapshot) => _current = snapshot;

        /// <summary>
        /// 內建預設規則：與動態規則上線前的固定行為相同（資安 Warning 以上、系統 Error 以上），
        /// 資料庫尚無規則或暫時讀不到時使用，確保告警不會因設定遺失而中斷。
        /// </summary>
        public static IReadOnlyList<AlertRule> DefaultRules() =>
        [
            new AlertRule
            {
                Name = "資安事件（Warning 以上）",
                Description = "所有資安日誌達 Warning 以上即告警（預設規則）",
                Priority = 100,
                LogType = AlertRuleLogTypes.Security,
                MinLevel = "Warning",
                Action = AlertRuleActions.Alert,
                CreatedBy = "SYSTEM"
            },
            new AlertRule
            {
                Name = "系統錯誤（Error 以上）",
                Description = "所有系統日誌達 Error 以上即告警（預設規則）",
                Priority = 200,
                LogType = AlertRuleLogTypes.System,
                MinLevel = "Error",
                Action = AlertRuleActions.Alert,
                CreatedBy = "SYSTEM"
            }
        ];
    }

    public static class AlertRuleEngine
    {
        public static bool TryParseLevel(string? value, out LogEventLevel level) =>
            Enum.TryParse(value, ignoreCase: true, out level) && Enum.IsDefined(level);

        /// <summary>預先過濾：此事件是否有機會被任何「Alert」規則命中（用於 Serilog Sink 前，避免所有日誌都進入佇列）</summary>
        public static bool IsCandidate(LogEvent logEvent, AlertRuleSnapshot snapshot)
        {
            var isSecurity = IsSecurityEvent(logEvent);
            var minimum = isSecurity ? snapshot.MinSecurity : snapshot.MinSystem;
            return minimum is not null && logEvent.Level >= minimum.Value;
        }

        /// <summary>依優先序找出第一條符合的規則；沒有符合者回傳 null（不告警）</summary>
        public static AlertRule? Match(AlertLogFacts facts, AlertRuleSnapshot snapshot)
        {
            foreach (var rule in snapshot.Rules)
            {
                if (IsMatch(rule, facts)) return rule;
            }
            return null;
        }

        public static AlertLogFacts ToFacts(LogEvent logEvent)
        {
            string? Scalar(string name) =>
                logEvent.Properties.TryGetValue(name, out var value) && value is ScalarValue scalar
                    ? scalar.Value?.ToString()?.Trim('"')
                    : null;

            return new AlertLogFacts(
                IsSecurityEvent(logEvent),
                logEvent.Level,
                logEvent.RenderMessage(),
                Scalar("ModuleName"),
                Scalar("Operation"),
                logEvent.Exception?.GetType().FullName);
        }

        public static bool IsSecurityEvent(LogEvent logEvent) =>
            logEvent.Properties.TryGetValue("LogType", out var value) &&
            value is ScalarValue scalar &&
            "Security".Equals(scalar.Value?.ToString(), StringComparison.OrdinalIgnoreCase);

        private static bool IsMatch(AlertRule rule, AlertLogFacts facts)
        {
            var expectedType = facts.IsSecurity ? AlertRuleLogTypes.Security : AlertRuleLogTypes.System;
            if (rule.LogType != AlertRuleLogTypes.Any && rule.LogType != expectedType) return false;

            if (!TryParseLevel(rule.MinLevel, out var minLevel) || facts.Level < minLevel) return false;

            return Contains(facts.Message, rule.MessageContains)
                   && Contains(facts.Module, rule.ModuleContains)
                   && Contains(facts.Operation, rule.OperationContains)
                   && Contains(facts.ExceptionType, rule.ExceptionContains);
        }

        // 條件未填視為不限制；有填則需包含（不分大小寫）
        private static bool Contains(string? source, string? expected) =>
            string.IsNullOrWhiteSpace(expected) ||
            (source is not null && source.Contains(expected.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
