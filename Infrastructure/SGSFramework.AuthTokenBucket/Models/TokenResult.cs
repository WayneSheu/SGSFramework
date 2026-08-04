using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Models
{

    /// <summary>
    /// 封裝 Refresh Token 執行變更與輪轉後的最終成果
    /// </summary>
    public sealed class TokenResult
    {
        /// <summary>
        /// 新產生的 Access Token (JWT)，內嵌最新的 Bitmask 權限與使用者識別
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// 經由 Token Rotation 機制產生的全新、單次使用的加密 Refresh Token
        /// </summary>
        public string RefreshToken { get; set; } = string.Empty;

        /// <summary>
        /// 標記此 Refresh Token 在資料庫中的絕對過期時間（通常為 UtcNow + 7 天）
        /// 用於前端判斷何時必須強制引導使用者重新登入
        /// </summary>
        public DateTime ExpiresAt { get; set; }
    }
}
