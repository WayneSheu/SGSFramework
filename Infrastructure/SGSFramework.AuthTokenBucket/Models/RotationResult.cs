using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Models
{
    /// <summary>
    /// 雙票輪轉的結果物件，包含輪轉狀態、生成的新 Token 雜湊值以及過期時間等資訊
    /// </summary>
    public sealed class RotationResult
    {
        // 輪轉結果狀態，指示是成功生成新 Token 還是處於寬限期內允許複用
        public RotationStatus Status { get; set; }
        // 當 Status 為 Success 時，這裡會包含新生成的 Token 雜湊值；當 Status 為 GracePeriodMatch 時，則是上一次成功輪轉的 Token 雜湊值
        public string TokenHash { get; set; } = string.Empty;
        // 新 Token 的絕對過期時間，前端可以根據這個時間來決定何時再次發起輪轉請求
        public DateTime ExpiresAt { get; set; }
    }

}
