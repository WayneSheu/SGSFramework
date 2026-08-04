using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Enterprise.HttpSdk.Handlers
{
    /// <summary>
    /// LoggingHandler is a custom HttpMessageHandler that logs details about HTTP requests and responses.
    /// LoggingHandler 是一個自訂的 HttpMessageHandler，用於記錄 HTTP 請求和回應的詳細資訊。
    /// </summary>
    public class LoggingHandler : DelegatingHandler
    {
        private readonly ILogger<LoggingHandler> _logger;

        public LoggingHandler(ILogger<LoggingHandler> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Sending HTTP request: {Method} {Uri}", request.Method, request.RequestUri);

            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                stopwatch.Stop();

                _logger.LogInformation("Received HTTP response: {StatusCode} in {ElapsedMs}ms", response.StatusCode, stopwatch.ElapsedMilliseconds);
                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "HTTP request failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
