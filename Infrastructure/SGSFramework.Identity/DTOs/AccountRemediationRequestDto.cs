using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs
{
    /// <summary>
    /// 帳號身分補償處置請求 DTO
    /// </summary>
    public sealed record AccountRemediationRequestDto
    {
        public string UserId { get; init; } = string.Empty;
        public string? VerificationMethod { get; init; }
    }
}
