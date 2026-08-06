using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    /// <summary>
    /// 帳號身分補償處置回應 DTO
    /// </summary>
    public sealed record AccountRemediationResponseDto
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public DateTime RemediatedAtUtc { get; init; }
    }
}
