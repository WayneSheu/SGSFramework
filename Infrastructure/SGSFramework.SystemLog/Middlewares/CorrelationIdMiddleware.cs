using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace SGSFramework.SystemLog.Middlewares
{
    /// <summary>
    /// Middleware for generating and propagating a Correlation ID across requests.
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        private const string CorrelationIdHeaderKey = "X-Correlation-ID";

        public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

        private static bool IsValid(string value) =>
            value.Length is > 0 and <= 50 && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

        public async Task Invoke(HttpContext context)
        {
            // 1. 嘗試從 Header 取得，沒有就產新的
            // 用戶端提供的值需通過長度與字元檢查（資料庫欄位上限 50，且避免日誌注入），否則改產新的
            string correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeaderKey, out var headerValue) &&
                                   IsValid(headerValue.ToString())
                ? headerValue.ToString()
                : Guid.NewGuid().ToString();

            // 2. 存入 HttpContext.Items 或 TraceIdentifier，確保後續可用
            context.TraceIdentifier = correlationId;

            // 3. 將 CorrelationId 寫回 Response Header (方便前端除錯)
            // 使用索引器替代 .Add()
            context.Response.Headers[CorrelationIdHeaderKey] = correlationId;
            // 4. 使用 Serilog 的 LogContext 推入屬性，這會讓該請求內的所有 Log 自動帶上此 ID
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }
    }
}
