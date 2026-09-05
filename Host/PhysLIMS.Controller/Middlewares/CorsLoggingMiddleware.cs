// Path: src/SGSFramework/ApiInfrastructure/Middlewares/CorsLoggingMiddleware.cs
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;

namespace SGSFramework.ApiInfrastructure.Middlewares;

public class CorsLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorsLoggingMiddleware> _logger;

    public CorsLoggingMiddleware(RequestDelegate next, ILogger<CorsLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        var origin = context.Request.Headers["Origin"].ToString();

        // 僅針對帶有 Origin Header 的跨域請求進行監控
        if (!string.IsNullOrEmpty(origin))
        {
            var allowedOrigins = configuration
                .GetSection("CorsSettings:AllowedOrigins")
                .Get<string[]>() ?? Array.Empty<string>();

            // 檢查 Origin 是否在允許清單中
            bool isAllowed = allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);

            if (!isAllowed)
            {
                var requestPath = context.Request.Path;
                var requestMethod = context.Request.Method;
                var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown IP";

                _logger.LogWarning(
                    " Security Warning: CORS Request Blocked! Origin: {Origin} | Method: {Method} | Path: {Path} | Client IP: {ClientIp}",
                    origin,
                    requestMethod,
                    requestPath,
                    clientIp
                );
            }
        }

        await _next(context);
    }
}