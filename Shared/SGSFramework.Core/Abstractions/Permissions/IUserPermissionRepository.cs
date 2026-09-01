using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Permissions
{
    /// <summary>
    /// 使用者權限與 64 位元遮罩資料存取介面
    /// </summary>
    public interface IUserPermissionRepository
    {
        Task<Dictionary<string, long>> GetPermissionsByLabAsync(string userId, Guid labId, CancellationToken cancellationToken = default);
        Task<Dictionary<string, long>> GetGlobalPermissionsAsync(string userId, CancellationToken cancellationToken = default);

        Task<bool> SaveUserLabPermissionsAsync(
            string userId,
            Guid labId,
            Dictionary<string, long> permissions,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 儲存或更新使用者的全域/組織級 64 位元遮罩權限對應表
        /// </summary>
        Task<bool> SaveUserGlobalPermissionsAsync(
            string userId,
            Dictionary<string, long> permissions,
            CancellationToken cancellationToken = default);
    }
}
