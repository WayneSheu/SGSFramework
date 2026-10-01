using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.RefreshTokens
{
    /// <summary>
    /// Token 刷新請求 DTO
    /// </summary>
    public sealed record RefreshTokenRequestDto
    {
        public string AccessToken { get; init; } = string.Empty;
        public string RefreshToken { get; init; } = string.Empty;
    }
}
