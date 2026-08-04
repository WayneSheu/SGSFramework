using Microsoft.AspNetCore.Http;
using SGSFramework.Core.Identiies.Tenants;

namespace SGSFramework.Persistent.Tenants
{
    public class TenantService(IHttpContextAccessor httpContextAccessor) : ITenantService
    {
        public string GetTenantId()
        {
            var tenantId = httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Id"].FirstOrDefault();
            return tenantId ?? throw new InvalidOperationException("Tenant ID header is missing.");
        }

        // 取得資料庫位置
        public string GetConnectionString()
        {
            return string.Empty;
        }

        public string GetSchemaName()
        {
            return string.Empty;
        }

    }
}
