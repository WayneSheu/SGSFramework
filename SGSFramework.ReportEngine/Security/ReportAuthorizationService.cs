using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace SGSFramework.ReportEngine.Security
{
    /// <summary>
    /// 基於 Claim 的報表權限驗證實作
    /// </summary>
    public class ReportAuthorizationService : IReportAuthorizationService
    {
        private readonly ILogger<ReportAuthorizationService> _logger;
        private const string ReportClaimType = "Permission";

        public ReportAuthorizationService(ILogger<ReportAuthorizationService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> AuthorizeAsync(ClaimsPrincipal user, string reportTypeCode)
        {
            if (user?.Identity == null || !user.Identity.IsAuthenticated)
            {
                _logger.LogWarning("未授權的使用者嘗試存取報表: {ReportTypeCode}", reportTypeCode);
                return await Task.FromResult(false);
            }

            // 檢查使用者是否擁有對應報表代碼的 Claim 權限
            var hasPermission = user.HasClaim(c => c.Type == ReportClaimType && c.Value.Equals(reportTypeCode, StringComparison.OrdinalIgnoreCase));

            if (!hasPermission)
            {
                _logger.LogWarning("使用者 {UserName} 缺乏存取報表類別 {ReportTypeCode} 的權限", user.Identity.Name, reportTypeCode);
            }

            return await Task.FromResult(hasPermission);
        }
    }
}
