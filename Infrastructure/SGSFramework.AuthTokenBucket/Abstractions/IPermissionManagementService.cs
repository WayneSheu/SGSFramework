// 檔案路徑: Abstractions/SGSFramework.AuthTokenBucket.Abstractions/IPermissionManagementService.cs

using SGSFramework.AuthTokenBucket.DTOs;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.AuthTokenBucket.Abstractions
{
    public interface IPermissionManagementService
    {
        Task<bool> HasPermissionAsync(string userId, string permissionCode, CancellationToken cancellationToken = default);
        Task GrantPermissionToRoleAsync(string roleId, string permissionCode, CancellationToken cancellationToken = default);
        Task RevokePermissionFromRoleAsync(string roleId, string permissionCode, CancellationToken cancellationToken = default);

        // 🔑 補上 Controller 呼叫的方法宣告
        Task<List<PermissionModuleDto>> GetPermissionTreeAsync(CancellationToken cancellationToken = default);
        Task<RolePermissionMatrixDto?> GetRolePermissionsAsync(string roleId, CancellationToken cancellationToken = default);
        Task<(bool Succeeded, string Message)> UpdateRolePermissionsAsync(UpdateRolePermissionsRequest request, CancellationToken cancellationToken = default);
    }
}