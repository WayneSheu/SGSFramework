using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace SGSFramework.Core.HttpAuditProviders
{
    /// <summary>
    /// 基於 HTTP 上下文的審計軌跡提供者
    /// 負責為追溯系統、日誌系統以及總帳模組提供實時的身分與環境快照。
    /// </summary>
    public class HttpAuditProvider : IAuditProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public HttpAuditProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public string? UserId => _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
        public string? UserName => _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value;
        public string? TraceId => _httpContextAccessor.HttpContext?.TraceIdentifier;
        public string? RemoteIp => _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        public string? DeviceId => _httpContextAccessor.HttpContext?.Request.Headers["X-Device-Id"].ToString() ?? "Unknown_Http_Device";
        public string? LaboratoryId => _httpContextAccessor.HttpContext?.User?.FindFirst("LaboratoryId")?.Value;
    
    
    
    }
}
