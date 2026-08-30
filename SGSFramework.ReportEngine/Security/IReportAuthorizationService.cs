using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace SGSFramework.ReportEngine.Security
{
    /// <summary>
    /// 報表權限控制介面
    /// </summary>
    public interface IReportAuthorizationService
    {
        /// <summary>
        /// 驗證當前使用者是否具備存取特定報表類別的權限
        /// </summary>
        Task<bool> AuthorizeAsync(ClaimsPrincipal user, string reportTypeCode);
    }
}
