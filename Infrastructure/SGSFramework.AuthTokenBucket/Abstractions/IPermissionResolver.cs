using Microsoft.AspNetCore.Identity;
using SGSFramework.Core.Abstractions.Entities.Identities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Abstractions
{
    /// <summary>
    /// 使用者動態權限與最高管理者策略解析介面
    /// </summary>
    public interface IPermissionResolver
    {
        /// <summary>
        /// 解析指定使用者的 Bitmask 權限遮罩字串與系統管理員身份
        /// </summary>
        /// <typeparam name="TUser">使用者實體類型</typeparam>
        /// <param name="user">當前使用者實體</param>
        /// <param name="userManager">Identity 使用者管理員</param>
        /// <returns>包含 Bitmask 遮罩字串與是否為系統管理員之元組</returns>
        Task<(string PermissionMask, bool IsAdmin)> ResolveUserPermissionsAsync<TUser>(
            TUser user,
            UserManager<TUser> userManager) where TUser : ApplicationUser, new();
    }
}
