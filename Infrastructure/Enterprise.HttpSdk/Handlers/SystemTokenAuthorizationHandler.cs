using System.Net;
using System.Net.Http.Headers;
using Enterprise.HttpSdk.Interfaces;

namespace Enterprise.HttpSdk.Handlers
{
    /// <summary>
    /// 服務間呼叫自動附加系統帳號 JWT（Authorization: Bearer）。
    /// 收到 401（權杖被撤銷、Scope 異動或快取突發失效）時強制重新換發並重送一次。
    /// </summary>
    public sealed class SystemTokenAuthorizationHandler : DelegatingHandler
    {
        private readonly ISystemAccountTokenService _tokenService;
        private readonly string _accountName;

        public SystemTokenAuthorizationHandler(ISystemAccountTokenService tokenService, string accountName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _accountName = accountName;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // 1. 先使用記憶體快取的權杖，避免每次呼叫都換發
            var token = await _tokenService.GetSystemTokenAsync(_accountName, forceRefresh: false, cancellationToken).ConfigureAwait(false);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            // 2. 401：強制換新權杖再試一次（僅一次，避免金鑰錯誤時無限重試）
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                token = await _tokenService.GetSystemTokenAsync(_accountName, forceRefresh: true, cancellationToken).ConfigureAwait(false);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }

            return response;
        }
    }
}
