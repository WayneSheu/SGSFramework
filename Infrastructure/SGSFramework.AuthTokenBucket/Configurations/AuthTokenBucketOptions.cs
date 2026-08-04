using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Configurations
{
    /// <summary>
    /// 這個類別用於封裝 JWT Token 的相關設定，包括密鑰、發行者、受眾、過期時間等。
    /// </summary>
    public sealed class AuthTokenBucketOptions
    {
        
        public string SecretKey { get; set; } = string.Empty;// 用於簽署 JWT Token 的對稱密鑰，必須至少 16 字元以確保安全性
        public string Issuer { get; set; } = string.Empty;// JWT Token 的發行者
        public string Audience { get; set; } = string.Empty;// JWT Token 的發行者
        public int AccessTokenExpirationMinutes { get; set; } = 15;// Access Token 的預設過期時間為 15 分鐘
        public int RefreshTokenExpirationDays { get; set; } = 7;// Refresh Token 的預設過期時間為 7 天
        /// <summary>
        /// 多 bottle/分頁 併發換票的重複使用安全寬限期（秒）
        /// 預設值設為 8 秒，符合大多數網際網路延遲模型
        /// </summary>
        public int RefreshTokenGracePeriodSeconds { get; set; } = 8;
        public int MaxDeviceCount { get; set; } = 6; // 多裝置主動控管上限


    }
}
