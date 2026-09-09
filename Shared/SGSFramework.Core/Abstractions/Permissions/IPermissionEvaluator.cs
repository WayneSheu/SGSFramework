using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace SGSFramework.Core.Abstractions.Permissions
{
    /// <summary>
    /// 權限判定評估器介面
    /// </summary>
    public interface IPermissionEvaluator
    {
        /// <summary>
        /// 評估當前使用者是否擁有特定權限點（支援超級管理員特權自動過關）
        /// </summary>
        /// <param name="user">當前 ClaimsPrincipal</param>
        /// <param name="requiredPermission">必備權限點代碼 (例: SYS.MENU.READ)</param>
        /// <param name="cancellationToken">取消權牌</param>
        /// <returns>驗證結果</returns>
        Task<bool> HasPermissionAsync(ClaimsPrincipal user, string requiredPermission, CancellationToken cancellationToken = default);
    }
}
