using Microsoft.AspNetCore.Http;
using Serilog.Context;
using SGSFramework.Core.Tracing;

namespace SGSFramework.SystemLog.Middlewares
{
    /// <summary>
    /// 為每個請求決定統一追蹤碼並傳遞到所有日誌：
    /// 系統日誌 (LogContext.CorrelationId)、資安日誌 (CorrelationContext)、資料日誌 (HttpContext.TraceIdentifier)、
    /// 錯誤回應 (ProblemDetails.traceId) 與回應標頭 X-Correlation-ID 皆為同一個值。
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

        public async Task Invoke(HttpContext context)
        {
            // 1. 用戶端帶入的值需能轉為標準格式（GUID 或 32 碼十六進位），否則視為無效並改產新的（避免超長值與日誌注入）
            var correlationId =
                CorrelationContext.Normalize(context.Request.Headers[CorrelationContext.HeaderName].FirstOrDefault())
                ?? CorrelationContext.NewId();

            // 2. TraceIdentifier 供 HttpAuditProvider（資料日誌）與 GlobalExceptionHandler 使用
            context.TraceIdentifier = correlationId;
            context.Items["CorrelationId"] = correlationId;

            // 3. 回應標頭，方便前端與使用者回報問題時提供追蹤碼
            context.Response.Headers[CorrelationContext.HeaderName] = correlationId;

            // 4. 以 Activity Baggage 帶著追蹤碼，對外呼叫的 HttpClient handler（ClientCorrelationHandler）據此附加
            //    X-Correlation-ID；Baggage 也會隨 W3C 傳播標頭送往支援的下游服務
            System.Diagnostics.Activity.Current?.AddBaggage("correlation_id", correlationId);

            using (CorrelationContext.Begin(correlationId))
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }
    }
}
