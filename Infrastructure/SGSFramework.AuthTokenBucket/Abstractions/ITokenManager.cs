using Microsoft.AspNetCore.Identity;
using SGSFramework.Core.Abstractions.Entities.Identities;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Abstractions
{
    /// <summary>
    /// 核心 JWT Token 管理器介面
    /// </summary>
    public interface ITokenManager
    {
        /// <summary>
        /// 生成安全簽章之 JWT Access Token
        /// </summary>
        string GenerateAccessToken(IdentityUser<Guid> user, string bitmaskString, string deviceId);

        /// <summary>
        /// 簽發包含角色與系統管理員身分宣告的高權限 Access Token
        /// </summary>
        string GenerateAccessToken(IdentityUser<Guid> user, string bitmaskString, string deviceId, IList<string>? roles, bool isSystemAdmin);

        /// <summary>
        /// 從過期 Token 安全提取 ClaimsPrincipal 身分
        /// </summary>
        ClaimsPrincipal? GetPrincipalFromExpiredToken(string token, string? clientIp = null);
    }
}
