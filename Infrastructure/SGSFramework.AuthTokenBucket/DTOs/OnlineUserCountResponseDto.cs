using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs
{
    /// <summary>
    /// 線上人數統計回應 DTO
    /// </summary>
    public sealed record OnlineUserCountResponseDto
    {
        public int ActiveOnlineUsers { get; init; }
        public int ObservationWindowMinutes { get; init; }
        public bool IsUsingDefaultConfigWindow { get; init; }
        public DateTime CalculatedAtUtc { get; init; }
    }
}
