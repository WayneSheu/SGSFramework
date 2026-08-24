using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace SGSFramework.Core.Identiies.CurrentUser
{
    /// <summary>
    /// 當前使用者資訊解析服務
    /// </summary>
    /// <summary>
    /// 當前使用者資訊解析服務
    /// </summary>
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

        public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

        // 1. UserId：優先抓取 NameIdentifier，若無則降級讀取 JWT 標準 sub (Subject)[cite: 1, 6]
        public string? UserId => GetClaimValue(ClaimTypes.NameIdentifier)
                                 ?? GetClaimValue(JwtRegisteredClaimNames.Sub);

        // 2. Strong-typed UserGuid
        public Guid UserGuid => Guid.TryParse(UserId, out var guid) ? guid : Guid.Empty;

        // 3. UserName：優先抓取 Name，若無則降級讀取 preferred_username 或 JWT name
        public string? UserName => GetClaimValue(ClaimTypes.Name)
                                   ?? GetClaimValue("preferred_username")
                                   ?? GetClaimValue(JwtRegisteredClaimNames.Name);

        // 4. Email
        public string? Email => GetClaimValue(ClaimTypes.Email)
                                ?? GetClaimValue(JwtRegisteredClaimNames.Email);

        // 5. TenantId：解析自訂 Claim "tenant_id" 或 "tenant"
        public string? TenantId => GetClaimValue("tenant_id")
                                  ?? GetClaimValue("tenant");

        // 6. 角色清單 (Roles)
        public IReadOnlyList<string> Roles => User?.FindAll(ClaimTypes.Role)
                                                  .Select(c => c.Value)
                                                  .ToList()
                                              ?? (IReadOnlyList<string>)Array.Empty<string>();

        // 7. 權限點清單 (Permissions)
        public IReadOnlyList<string> Permissions => User?.FindAll("permission")
                                                         .Select(c => c.Value)
                                                         .ToList()
                                                     ?? (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>
        /// 依據指定之 ClaimType 提取 Claim Value
        /// </summary>
        public string? GetClaimValue(string claimType)
        {

            if (string.IsNullOrWhiteSpace(claimType)) return null;

            return User?.FindFirst(claimType)?.Value;
        }
    }
}
