// 檔案路徑: Abstractions/SGSFramework.AuthTokenBucket.Abstractions/IPermissionManagementService.cs

using SGSFramework.AuthTokenBucket.DTOs;
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
        Task<List<PermissionModuleDto>> GetPermissionTreeAsync(CancellationToken cancellationToken = default);
        Task<RolePermissionMatrixDto?> GetRolePermissionsAsync(string roleId, CancellationToken cancellationToken = default);
        Task<(bool Succeeded, string Message)> UpdateRolePermissionsAsync(UpdateRolePermissionsRequest request, CancellationToken cancellationToken = default);

    }
}