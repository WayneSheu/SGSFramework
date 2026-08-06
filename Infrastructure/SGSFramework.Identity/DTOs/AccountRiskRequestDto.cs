using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    /// <summary>
    /// 帳號風險熔斷處置請求 DTO
    /// </summary>
    public sealed record AccountRiskRequestDto
    {
        public string UserId { get; init; } = string.Empty;
        public string? Reason { get; init; }
    }
}
