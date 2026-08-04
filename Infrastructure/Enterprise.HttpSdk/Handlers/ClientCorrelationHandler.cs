using System;
using System.Collections.Generic;
using System.Text;

namespace Enterprise.HttpSdk.Handlers
{
    /// <summary>
    /// ClientCorrelationHandler 用於在 HTTP 請求中添加 Correlation ID。此 ID 可以用於追蹤和診斷請求的流程。
    /// </summary>
    public class ClientCorrelationHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // 模擬或從鏈結內容中取得 Correlation ID
            string correlationId = Guid.NewGuid().ToString();

            if (!request.Headers.Contains("X-Correlation-ID"))
            {
                request.Headers.Add("X-Correlation-ID", correlationId);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
