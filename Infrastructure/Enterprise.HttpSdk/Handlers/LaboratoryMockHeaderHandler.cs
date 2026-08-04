using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace Enterprise.HttpSdk.Handlers
{
    /// <summary>
    /// 企業級實驗室與開發期 Windows AD 身分模擬安全攔截處理器
    /// </summary>
    public sealed class LaboratoryMockHeaderHandler : DelegatingHandler
    {
        private readonly IWebAssemblyHostEnvironment _environment;

        // 🚀 透過 DI 注入環境介面，用以判定開發情境
        public LaboratoryMockHeaderHandler(IWebAssemblyHostEnvironment environment)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                // 1. 業務上下文中不論環境皆需攜帶的標頭 (例如當前切換的實驗室 ID)
                string laboratoryId = "LAB-PHYSIS-2026";

                if (!string.IsNullOrWhiteSpace(laboratoryId))
                {
                    request.Headers.Remove("X-Laboratory-Id");
                    request.Headers.Add("X-Laboratory-Id", laboratoryId);
                }

                // 2. 🚀 資安隔離：僅在開發環境 (IsDevelopment) 下才允許注入 Windows AD 模擬標頭
                if (_environment.IsDevelopment())
                {
                    string mockUser = "Shanda\\Wayne"; // 僅開發期生效的模擬帳號

                    if (!string.IsNullOrWhiteSpace(mockUser))
                    {
                        request.Headers.Remove("X-Mock-Windows-User");
                        request.Headers.Add("X-Mock-Windows-User", mockUser);
                    }
                }
                else
                {
                    // 生產環境嚴格防禦：若請求因快取或其他因素殘留此標頭，強制肅清
                    request.Headers.Remove("X-Mock-Windows-User");
                }
            }
            catch (Exception ex)
            {
                // 系統核心異常防禦紀錄
                System.Diagnostics.Debug.WriteLine($"[HeaderHandler_Exception] 處理自訂標頭時發生異常: {ex.Message}");
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }

}
