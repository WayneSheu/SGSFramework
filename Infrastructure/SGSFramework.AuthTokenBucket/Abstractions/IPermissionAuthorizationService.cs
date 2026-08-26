using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Abstractions
{
    /// <summary>
    /// 權限授權驗證服務介面（Runtime Read-Only / Query Side），專注於高效能請求過濾與 BitMask/Claims 檢核。
    /// </summary>
    public interface IPermissionAuthorizationService
    {
        /// <summary>
        /// 驗證當前請求之 ClaimsPrincipal 是否具備指定的權限鍵。
        /// </summary>
        Task<bool> HasPermissionAsync(ClaimsPrincipal user, string permissionKey, CancellationToken cancellationToken = default);
    }
}
