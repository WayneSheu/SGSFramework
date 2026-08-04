using Microsoft.AspNetCore.Http;
using SGSFramework.AuthTokenBucket.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Extensions
{
    public static class HttpContextExtensions
    {
        /// <summary>
        /// 從當前 HTTP 請求標頭與環境變數中，自動擷取標準化 DeviceInfo
        /// </summary>
        public static DeviceInfo GetClientDeviceInfo(this HttpContext context, string deviceIdFromHeader)
        {
            // 1. 取得客戶端真實 IP (考慮 Reverse Proxy 如 Nginx/Cloudflare 的標頭)
            string ip = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()
                        ?? context.Connection.RemoteIpAddress?.ToString()
                        ?? "0.0.0.0";

            // 2. 取得瀏覽器 User-Agent 並進行簡易解析作為 DeviceName
            string userAgent = context.Request.Headers["User-Agent"].FirstOrDefault() ?? "Unknown Device";
            string deviceName = ParseUserAgent(userAgent);


            // 3. 取得客戶端語系，並統一轉為標準格式（如 zh-TW）
            string rawCulture = context.Request.Headers["Accept-Language"].FirstOrDefault()?.Split(',')[0] ?? "zh-TW";

            // 處理大小寫不一致的問題（例如將 zh-tw 轉為 zh-TW）
            string culture = rawCulture.Equals("zh-tw", StringComparison.OrdinalIgnoreCase)
                ? "zh-TW"
                : rawCulture;

            return new DeviceInfo
            {
                DeviceId = string.IsNullOrWhiteSpace(deviceIdFromHeader) ? Guid.NewGuid().ToString("N") : deviceIdFromHeader,
                DeviceName = deviceName,
                ClientIp = ip,
                ClientCulture = culture
            };
        }

        private static string ParseUserAgent(string ua)
        {
            if (ua.Contains("Edg")) return "Edge Browser";
            if (ua.Contains("Chrome")) return "Chrome Browser";
            if (ua.Contains("Safari") && !ua.Contains("Chrome")) return "Safari Browser";
            if (ua.Contains("Firefox")) return "Firefox Browser";
            return ua.Length > 50 ? ua[..50] : ua; // 截斷過長字串
        }
    }
}
