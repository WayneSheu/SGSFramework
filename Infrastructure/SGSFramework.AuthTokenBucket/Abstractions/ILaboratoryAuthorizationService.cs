using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Abstractions
{
    /// <summary>
    /// 狀體化實驗室認證服務接口
    /// </summary>
    public interface ILaboratoryAuthorizationService
    {
        /// <summary>
        /// 驗證使用者的目前實驗室其 Parent.Code 是否符合允許的類別清單
        /// </summary>
        Task<bool> ValidateUserLaboratoryCategoryAsync(ClaimsPrincipal user, string[] allowedCategories, CancellationToken cancellationToken);
    }
}
