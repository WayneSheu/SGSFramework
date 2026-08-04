using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Models
{
    /// <summary>
    /// 前端發起雙票輪轉時的資料傳輸物件 (DTO)
    /// </summary>
    public sealed class RefreshRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string OldRefreshToken { get; set; } = string.Empty;
    }
}
