using Enterprise.HttpSdk.Interfaces;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Enterprise.HttpSdk.Handlers
{
    /// <summary>
    /// 全域 HTTP 請求攔截器：自動為外發請求附加 JWT 驗證標頭、主動檢查效期與被動感應無感刷新
    /// </summary>
    public sealed class AuthenticationHeaderHandler : DelegatingHandler
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly IAuthService _authService; //依賴注入認證服務
        private const string TokenKey = "SES_ACCESS_TOKEN";
        private readonly JwtSecurityTokenHandler _jwtHandler = new(); // 複用 Handler 以提升效能

        public AuthenticationHeaderHandler(IJSRuntime jsRuntime, IAuthService authService)
        {
            _jsRuntime = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            string? token = null;

            try
            {
                // 從瀏覽器安全儲存區提取儲存的 Token
                token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, TokenKey);

                if (!string.IsNullOrWhiteSpace(token))
                {
                    //策略一：主動檢查（預先判斷 Token 是否即將過期）
                    if (IsTokenExpiredOrExpiringSoon(token))
                    {
                        System.Diagnostics.Debug.WriteLine("Access Token 即將過期，主動觸發無感刷新程序。");
                        token = await _authService.RefreshAccessTokenAsync(cancellationToken);
                    }
                }
            }
            catch (JSException)
            {
                // 防禦性處理：避免 Prerendering 階段或相依網頁尚未就緒時 JavaScript 呼叫崩潰
                System.Diagnostics.Debug.WriteLine("JavaScript interop is unavailable at this lifecycle stage.");
            }

            // 附加有效的 Token 到 Header
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            // 發送原始 HTTP 請求
            var response = await base.SendAsync(request, cancellationToken);

            // 策略二：被動感應（攔截後端因無效 Token 回傳的 401 Unauthorized）
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                System.Diagnostics.Debug.WriteLine("後端回傳 401，嘗試被動觸發 Token 刷新與二次重試。");

                // 釋放原本失敗的回應 I/O 資源
                response.Dispose();

                // 呼叫刷新機制換取新 Token
                var newToken = await _authService.RefreshAccessTokenAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(newToken))
                {
                    // 刷新成功，使用新 Token 更新外發請求的 Authorization Header
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);

                    //重試（Retry）：再次將完全相同的請求發送出去
                    return await base.SendAsync(request, cancellationToken);
                }
                else
                {
                    // 連 Refresh Token 也失效，強制登出並清除狀態
                    await _authService.HandleLogoutAsync();
                }
            }

            return response;
        }

        /// <summary>
        /// 主動檢查 Token 是否過期或即將過期（預留 10 秒 Clock Skew 寬限值）
        /// </summary>
        private bool IsTokenExpiredOrExpiringSoon(string token)
        {
            try
            {
                if (_jwtHandler.ReadToken(token) is not JwtSecurityToken jwtToken)
                {
                    return true;
                }

                //預留 10 秒緩衝時間（Buffer Time），避免網路延遲或微幅時鐘不對齊
                return jwtToken.ValidTo <= DateTime.UtcNow.AddSeconds(10);
            }
            catch
            {
                // 若解析失敗，視同無效、已過期權杖
                return true;
            }
        }
    }
}
