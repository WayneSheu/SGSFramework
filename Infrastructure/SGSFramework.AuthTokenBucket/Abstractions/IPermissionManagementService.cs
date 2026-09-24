// 檔案路徑: Abstractions/SGSFramework.AuthTokenBucket.Abstractions/IPermissionManagementService.cs

using SGSFramework.AuthTokenBucket.DTOs.PermissionGrants;
using SGSFramework.AuthTokenBucket.DTOs.PermissionTree;
using SGSFramework.AuthTokenBucket.DTOs.RolePermissions;
using SGSFramework.Core.Abstractions.Permissions.Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.AuthTokenBucket.Abstractions
{
    public interface IPermissionManagementService
    {
        //使用者權限點查詢 (基於 UserId 檢索資料庫/快取)
        Task<bool> HasPermissionAsync(string userId, string permissionCode, CancellationToken cancellationToken = default);

        //角色權限授與與撤銷 (Write Operations)
        Task GrantPermissionToRoleAsync(string roleId, string permissionCode, CancellationToken cancellationToken = default);
        Task RevokePermissionFromRoleAsync(string roleId, string permissionCode, CancellationToken cancellationToken = default);

        //後台 UI 權限矩陣與樹狀結構維護 (Management DTOs)
        /// <summary>
        /// 同步並更新系統 PermissionMetadata 資料表，並透過 DTO 投影回傳完整資料清單
        /// </summary>
        Task<IReadOnlyCollection<PermissionMetadataDto>> SyncPermissionMetadataAsync(CancellationToken cancellationToken = default);
        Task<List<PermissionModuleDto>> GetPermissionTreeAsync(CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 獲取角色權限矩陣 (Read Operations)
        /// </summary>
        /// <param name="roleId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<RolePermissionMatrixDto?> GetRoleGlobalPermissionsAsync(string roleId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 更新角色全域權限 (Write Operations)
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<(bool Succeeded, string Message)> UpdateRolePermissionsAsync(UpdateRoleGlobalPermissionsRequest request, CancellationToken cancellationToken = default);

    }
}