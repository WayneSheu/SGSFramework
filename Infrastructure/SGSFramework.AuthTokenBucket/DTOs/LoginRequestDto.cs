using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs
{
    /// <summary>
    /// 帳密登入請求 DTO
    /// </summary>
    public sealed record LoginRequestDto
    {
        /// <summary>
        /// 支援UserName或Email
        /// </summary>
        public string NameOrEmail { get; init; } = string.Empty;
        public string Password { get; init; } = string.Empty;
        public string? RequestedLabId {  get; init; } = string.Empty;
    }
}
