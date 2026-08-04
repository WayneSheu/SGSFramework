using Enterprise.HttpSdk.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Enterprise.HttpSdk.Handlers
{
    /// <summary>
    /// 企業級多租戶/多實驗室上下文資料隔離標頭攔截器
    /// </summary>
    public sealed class LaboratoryContextHandler : DelegatingHandler
    {
        private readonly ILaboratoryStateProvider _stateProvider;

        // 🚀 透過 DI 注入狀態管理器，確保前端切換實驗室時，此處能動態讀取最新 ID
        public LaboratoryContextHandler(ILaboratoryStateProvider stateProvider)
        {
            _stateProvider = stateProvider ?? throw new ArgumentNullException(nameof(stateProvider));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                // 從動態狀態中取得當前活絡的實驗室/租戶 ID
                string currentLabId = _stateProvider.GetCurrentLaboratoryId();

                if (!string.IsNullOrWhiteSpace(currentLabId))
                {
                    if (request.Headers.Contains("X-Laboratory-Id"))
                    {
                        request.Headers.Remove("X-Laboratory-Id");
                    }
                    request.Headers.Add("X-Laboratory-Id", currentLabId);
                }
            }
            catch (Exception ex)
            {
                // 基礎設施層防禦日誌
                System.Diagnostics.Debug.WriteLine($"[TenantHeader_Error] 注入租戶標頭時異常: {ex.Message}");
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }

}
