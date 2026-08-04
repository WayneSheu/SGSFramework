using Microsoft.AspNetCore.Http;
using SGSFramework.Core.HttpAuditProviders;
using System.Security.Claims;

namespace SGSFramework.Core.Identiies.CurrentUser.Providers
{
    public class JwtAuditProvider : IAuditProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public JwtAuditProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public string UserId => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "System";
        public string? UserName => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
        public string? TraceId => _httpContextAccessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();
        public string? RemoteIp => _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        public string? DeviceId => _httpContextAccessor.HttpContext?.User?.FindFirst("DeviceId")?.Value ?? "Unknown_Jwt_Device";
        public string? LaboratoryId => _httpContextAccessor.HttpContext?.User?.FindFirst("LaboratoryId")?.Value;
    }
}
