using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using System.Net;
using System.Security.Claims;

namespace SGSFramework.SystemLog.Middlewares
{

    /// <summary>
    /// 攔截所有 HTTP Request，並將上下文資訊 (IP, CorrelationId, UserId) 注入到 HttpContext/Request Scope。
    /// 這確保了下游的 Middleware 或 Controller 在記錄日誌時能夠透過 Enricher 或 IHttpContextAccessor 訪問這些上下文資料。
    /// </summary>
    public class LoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<LoggingMiddleware> _logger;

        // 使用 Constructor Injection 取得下一個 Middleware 和 Logger
        public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// 執行此方法來攔截所有請求，並修飾 Context。
        /// </summary>
        public async Task InvokeAsync(HttpContext context)
        {
            // --- 1. 擷取 Correlation ID (Req. 3) ---
            // 標準慣例是檢查 X-Request-Id 或透過主機頭資訊來獲取追蹤ID。
            const string correlationHeader = "X-Request-Id";
            string correlationId = context.Request.Headers[correlationHeader].FirstOrDefault() ?? Guid.NewGuid().ToString();

            //// 將 Correlation ID 寫入 Response Header，讓呼叫者知道本次請求的唯一識別碼。
            //context.Response.Headers.Add("X-Correlation-ID", correlationId);

            // 設定 Context Property (方便日後在 Middleware 或 Enricher 中獲取)
            // 注意: 這是一種慣例做法，確保資料可被下游元件讀到。
            //context.Items["CorrelationId"] = correlationId;

            // --- 2. 擷取 IP Address (Req. 3) ---
            // 使用 Connection.RemoteIpAddress 來獲取真實的來源 IP。
            IPAddress? ipAddress = context.Connection.RemoteIpAddress;
            string sourceIp = ipAddress?.ToString() ?? "Unknown";
            context.Items["IP"] = sourceIp;
            LogContext.PushProperty("IP", sourceIp);

            var url = $"{context.Request.Scheme}://{context.Request.Host}{context.Request.Path}{context.Request.QueryString}";
            context.Items["Url"] = url;
            LogContext.PushProperty("Url", url);

            // --- 3. 擷取 User Identity (Req. 3) ---
            // 從身份驗證系統獲取的 Claims 來推斷使用者資訊。
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
            context.Items["UserId"] = userId;
            LogContext.PushProperty("UserId", userId);

            var tenantId = context.User.FindFirst("tenant_id")?.Value ?? "UnknownTenant";
            context.Items["TenantId"] = tenantId;
            LogContext.PushProperty("TenantId", tenantId);

            // 將所有捕獲到的 Context 資料記錄下來，作為啟動日誌的一部分 (可選)
            _logger.LogInformation("[{CorrelationId}] Incoming Request: {Method} {Path}. Source IP: {SourceIp}, User ID: {UserId}",
                correlationId, context.Request.Method, context.Request.Path, sourceIp, userId);
            // --- 4. 執行下一個 Middleware (關鍵步驟) ---
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                // 雖然業務邏輯層會自己捕獲和記錄 Exception，但在這裡可以記錄整個 request 鏈的失敗。
                _logger.LogError(ex, "Middleware caught an exception during the request lifecycle for Correlation ID: {CorrelationId}", correlationId);
                throw; // 確保錯誤繼續傳播給後端處理機制
            }

            // Request 完成時的額外邏輯可以放在這裡 (例如：計算響應時間、記錄 HTTP Status Code)
        }
    }

    /// <summary>
    /// 這是用於在 Program.cs 中註冊該 Middleware 的擴充方法。
    /// </summary>
    public static class LoggingMiddlewareExtensions
    {
        /// <summary>
        /// 將上下文捕獲的 Middleware 加入到 ASP.NET Core Pipeline。
        /// 應放在所有處理請求的 middleware (如 Authentication, Authorization) **之前**執行，
        /// 以確保所有下游元件都能訪問 Contextual Data。
        /// </summary>
        public static IApplicationBuilder UseContextLogging(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<LoggingMiddleware>();
        }
    }

}

// =========================================================
// 📌 使用說明 (如何註冊此 Middleware):
// 在 Program.cs 或 Startup.Configure 方法中：
/*
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    // 確保這是放在前面，在其他路由和身份驗證機制之前執行。
    app.UseContextLogging(); // <-- 使用此擴充方法

    // ... 其他 Middleware (如 UseAuthentication(), UseRouting())
}
*/
