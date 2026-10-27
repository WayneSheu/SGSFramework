#nullable enable
using System.Diagnostics;
using System.Globalization;

namespace SGSFramework.Core.Tracing
{
    /// <summary>
    /// 全系統統一的追蹤碼（CorrelationId / TraceId）。
    /// 系統日誌、資安日誌、資料日誌與錯誤回應 (ProblemDetails.traceId) 都使用同一個值，
    /// 格式固定為 32 碼小寫十六進位、不含連字號（與 W3C TraceId 及 core.AuditLogs.TraceId char(32) 一致），
    /// 方便從任一筆日誌追朔同一次請求的使用者軌跡與資料異動。
    /// </summary>
    public static class CorrelationContext
    {
        public const string HeaderName = "X-Correlation-ID";
        public const int Length = 32;

        private static readonly AsyncLocal<string?> CurrentId = new();

        /// <summary>目前非同步流程（請求）的追蹤碼；請求外為 null</summary>
        public static string? Current => CurrentId.Value;

        /// <summary>設定目前流程的追蹤碼，Dispose 時還原</summary>
        public static IDisposable Begin(string correlationId)
        {
            var previous = CurrentId.Value;
            CurrentId.Value = correlationId;
            return new Scope(previous);
        }

        /// <summary>產生新的追蹤碼：優先沿用 W3C Activity 的 TraceId（可與 OpenTelemetry 串接），否則產生新 GUID</summary>
        public static string NewId()
        {
            var activity = Activity.Current;
            if (activity is { IdFormat: ActivityIdFormat.W3C } && activity.TraceId != default)
            {
                return activity.TraceId.ToHexString();
            }

            return Guid.NewGuid().ToString("N");
        }

        /// <summary>目前追蹤碼；請求外（背景作業）則產生新的</summary>
        public static string CurrentOrNew() => Current ?? NewId();

        /// <summary>
        /// 將外部傳入的值轉為標準格式；可接受含連字號的 GUID 或 32 碼十六進位，其餘（長度或字元不合）回傳 null。
        /// </summary>
        public static string? Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            var trimmed = value.Trim();
            if (Guid.TryParse(trimmed, out var guid)) return guid.ToString("N");

            if (trimmed.Length == Length && trimmed.All(Uri.IsHexDigit))
            {
                return trimmed.ToLower(CultureInfo.InvariantCulture);
            }

            return null;
        }

        private sealed class Scope(string? previous) : IDisposable
        {
            public void Dispose() => CurrentId.Value = previous;
        }
    }
}
