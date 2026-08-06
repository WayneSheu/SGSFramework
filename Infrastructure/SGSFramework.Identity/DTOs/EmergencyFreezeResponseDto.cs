using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    /// <summary>
    /// 帳號緊急熔斷回應 DTO
    /// </summary>
    public sealed record EmergencyFreezeResponseDto
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public DateTime FrozenAtUtc { get; init; }
    }
}
