using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs
{
    /// <summary>
    /// 登入回應 DTO
    /// </summary>
    public sealed record LoginResponseDto
    {
        public object? Menus { get; init; }
        public object? TokenData { get; init; }
        public string Message { get; init; } = string.Empty;
    }
}
