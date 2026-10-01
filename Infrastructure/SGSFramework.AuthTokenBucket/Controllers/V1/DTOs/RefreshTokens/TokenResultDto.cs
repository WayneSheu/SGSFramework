using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.RefreshTokens
{
    /// <summary>
    /// 權限票據簽發/刷新結果 DTO
    /// </summary>
    public sealed record TokenResultDto
    {
        public string AccessToken { get; init; } = string.Empty;
        public string RefreshToken { get; init; } = string.Empty;
        public int ExpiresIn { get; init; }
        public string TokenType { get; init; } = "Bearer";
    }
}
