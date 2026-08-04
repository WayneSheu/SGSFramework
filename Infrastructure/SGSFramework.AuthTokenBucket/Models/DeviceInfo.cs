using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Models
{
    /// <summary>
    /// 封裝客戶端裝置特徵與指紋資訊的資料載體 (DTO / Value Object)
    /// </summary>
    public sealed record DeviceInfo
    {
        /// <summary>
        /// 裝置唯一識別碼 (由前端 Vue 經由 Canvas/WebGL 瀏覽器特徵或 UUID 生成的 SHA-256 雜湊值)
        /// </summary>
        public string DeviceId { get; init; } = string.Empty;

        /// <summary>
        /// 易讀的裝置描述名稱 (例如: "Chrome on Windows 11", "Safari on iPhone")
        /// </summary>
        public string DeviceName { get; init; } = string.Empty;

        /// <summary>
        /// 發起請求的客戶端來源 IP 位址
        /// </summary>
        public string ClientIp { get; init; } = string.Empty;

        /// <summary>
        /// 選擇性欄位：操作系統語系或時區 (可用於多裝置管理中心的進階稽核顯示)
        /// </summary>
        public string? ClientCulture { get; init; }

        /// <summary>
        /// 靜態建構工廠：提供標準空物件，避免傳回 Null 造成強型別檢查失敗
        /// </summary>
        public static DeviceInfo Empty => new();
    }
}
