using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Abstractions
{
    /// <summary>
    /// 權限授權驗證服務介面，專注於高效能請求過濾與 BitMask/Claims 檢核。
    /// </summary>
    public interface IPermissionAuthorizationService
    {
        /// <summary>
        /// 檢查指定使用者是否具備指定的權限點
        /// </summary>
        /// <param name="user">使用者 ClaimsPrincipal</param>
        /// <param name="permissionKey">權限代碼 (例: SYSTEM.ROLEMANAGEMENT.READ)</param>
        /// <param name="cancellationToken">取消權杖</param>
        /// <returns>若具備權限或為最高管理者則回傳 true，否則回傳 false</returns>
        Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permissionKey, CancellationToken cancellationToken = default);
    }
}
