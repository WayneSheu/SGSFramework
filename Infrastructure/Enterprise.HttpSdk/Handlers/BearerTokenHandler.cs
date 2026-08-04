using Enterprise.HttpSdk.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;

namespace Enterprise.HttpSdk.Handlers
{
    /// <summary>
    /// Handles adding a Bearer token to the Authorization header of HTTP requests.
    /// 處理向 HTTP 請求的 Authorization 標頭新增 Bearer 令牌。
    /// </summary>
    public class BearerTokenHandler : DelegatingHandler
    {
        private readonly ITokenProvider _tokenProvider;

        public BearerTokenHandler(ITokenProvider tokenProvider)
        {
            _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = await _tokenProvider.GetTokenAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
