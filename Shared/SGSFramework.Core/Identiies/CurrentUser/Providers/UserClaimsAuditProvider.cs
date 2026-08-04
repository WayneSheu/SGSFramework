using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace SGSFramework.Core.Identiies.CurrentUser.Providers
{
    public class UserClaimsAuditProvider : IUserClaimsAuditProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserClaimsAuditProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public AuditIdentity GetCurrentIdentity()
        {
            var userId = GetCurrentUserId();
            var traceId = _httpContextAccessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();
            var tenantId = GetTenantId();
            var remoteIp = GetUserIp();
            var userName = GetUserName();
            return new AuditIdentity(
                UserId: userId,
                TraceId: traceId,
                TenantId: tenantId,
                RemoteIp: remoteIp,
                UserAgent: _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString(),
                MachineName: Environment.MachineName
            );
        }
        public string GetTenantId()
        {
            // 優先從 Claim 取得，若無則檢查 Header (例如 X-Tenant-Id)
            var user = _httpContextAccessor.HttpContext?.User;
            var tenantClaim = user?.FindFirst("tenant_id")?.Value;

            if (!string.IsNullOrEmpty(tenantClaim)) return tenantClaim;

            // 回退方案：從 Header 取得 (常用於 API Key 驗證場景)
            return _httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Id"].ToString() ?? "Default";
        }

        public string GetCurrentUserId() =>
            _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "System";

        public string GetUserIp() =>
            _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        public string GetUserName() =>
            _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Anonymous";
    }
}
