using Microsoft.AspNetCore.Http;
using SGSFramework.Core.HttpAuditProviders;

namespace SGSFramework.Core.Identiies.CurrentUser.Providers
{
    public class CookieAuditProvider : IAuditProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CookieAuditProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public string UserId => _httpContextAccessor.HttpContext?.Request.Cookies["UID"] ?? "Unknown";
        public string? UserName => _httpContextAccessor.HttpContext?.Request.Cookies["UserName"];
        public string? TraceId => _httpContextAccessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();
        public string? RemoteIp => _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        public string? DeviceId => _httpContextAccessor.HttpContext?.Request.Cookies["DeviceId"] ?? "Unknown_Cookie_Device";
        public string? LaboratoryId => _httpContextAccessor.HttpContext?.Request.Cookies["LaboratoryId"];
    }
}
