using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Alerts
{
    /// <summary>
    /// 防洪告警抑制器介面，定義告警過濾與狀態檢查行為。
    /// </summary>
    public interface IAlertFloodSuppressor
    {
        /// <summary>
        /// 評估特定告警是否應被抑制分發。
        /// </summary>
        /// <param name="alertKey">告警唯一特徵鍵 (FingerprintKey)</param>
        /// <param name="cooldown">冷卻視窗時間</param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns>若為 true 代表觸發防洪機制（應抑制發送）；若為 false 代表允許發送。</returns>
        Task<bool> ShouldSuppressAsync(string alertKey, TimeSpan cooldown, CancellationToken cancellationToken = default);
    }
}
