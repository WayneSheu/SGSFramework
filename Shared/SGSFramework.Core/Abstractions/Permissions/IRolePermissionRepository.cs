using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Permissions
{
    /// <summary>
    /// 角色權限與 64 位元遮罩資料存取介面
    /// </summary>
    public interface IRolePermissionRepository
    {
        /// <summary>
        /// 取得特定角色在指定實驗室/組織層級的 64 位元遮罩權限對應表
        /// </summary>
        /// <param name="roleId">角色識別碼</param>
        /// <param name="labId">實驗室/組織識別碼</param>
        /// <param name="cancellationToken">取消權牌</param>
        /// <returns>Key: PermissionKey (模組名稱/權限鍵), Value: Bitmask (64位元遮罩值)</returns>
        Task<Dictionary<string, long>> GetPermissionsByLabAsync(
            string roleId,
            Guid labId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 取得特定角色的全域/系統級 64 位元遮罩權限對應表
        /// </summary>
        /// <param name="roleId">角色識別碼</param>
        /// <param name="cancellationToken">取消權牌</param>
        /// <returns>Key: PermissionKey (模組名稱/權限鍵), Value: Bitmask (64位元遮罩值)</returns>
        Task<Dictionary<string, long>> GetGlobalPermissionsAsync(
            string roleId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 儲存或更新角色在指定實驗室/組織層級的 64 位元遮罩權限對應表
        /// </summary>
        /// <param name="roleId">角色識別碼</param>
        /// <param name="labId">實驗室/組織識別碼</param>
        /// <param name="permissions">Key: PermissionKey, Value: Bitmask</param>
        /// <param name="cancellationToken">取消權牌</param>
        /// <returns>是否執行成功</returns>
        Task<bool> SaveRoleLabPermissionsAsync(
            string roleId,
            Guid labId,
            Dictionary<string, long> permissions,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 儲存或更新角色的全域/系統級 64 位元遮罩權限對應表
        /// </summary>
        /// <param name="roleId">角色識別碼</param>
        /// <param name="permissions">Key: PermissionKey, Value: Bitmask</param>
        /// <param name="cancellationToken">取消權牌</param>
        /// <returns>是否執行成功</returns>
        Task<bool> SaveRoleGlobalPermissionsAsync(
            string roleId,
            Dictionary<string, long> permissions,
            CancellationToken cancellationToken = default);
    }
}
