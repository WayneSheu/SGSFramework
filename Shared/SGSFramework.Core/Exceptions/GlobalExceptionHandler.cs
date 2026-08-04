using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Exceptions
{
    /// <summary>
    /// 全局異常處理器：捕獲未處理的異常，統一記錄日誌並返回標準化的 ProblemDetails 響應
    /// </summary>
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IProblemDetailsService _problemDetails;

        public GlobalExceptionHandler(
            ILogger<GlobalExceptionHandler> logger,
            IProblemDetailsService problemDetails)
        {
            _logger = logger;
            _problemDetails = problemDetails;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            // 1) Log with correlation
            _logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", httpContext.TraceIdentifier);

            // 2) Map exception → HTTP status + title/type
            var (status, title) = exception switch
            {
                KeyNotFoundException => (StatusCodes.Status404NotFound, "Resource Not Found"),
                ArgumentException => (StatusCodes.Status400BadRequest, "Invalid Request"),
                _ => (StatusCodes.Status500InternalServerError, "Server Error")
            };

            // 3) Build ProblemDetails (don’t leak internals in prod)
            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Type = exception.GetType().Name,
                Detail = httpContext.RequestServices
                                     .GetRequiredService<IHostEnvironment>()
                                     .IsDevelopment()
                         ? exception.Message
                         : null,
                Instance = httpContext.Request.Path
            };

            // 4) Enrich universally useful metadata
            problem.Extensions["traceId"] = httpContext.TraceIdentifier;
            problem.Extensions["timestamp"] = DateTime.UtcNow;

            // 5) Write response
            await _problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problem,
            });

            // Tell the pipeline we handled it
            return true;
        }
    }
}
