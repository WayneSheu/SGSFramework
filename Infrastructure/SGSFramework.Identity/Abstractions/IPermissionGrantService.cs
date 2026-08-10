using SGSFramework.Identity.DTOs;
using SGSFramework.Identity.DTOs.PermissionGrants;

namespace SGSFramework.Identity.Abstractions
{
    /// <summary>
    /// 角色實驗室維度 BitMask 權限管理服務介面
    /// </summary>
    public interface IPermissionGrantService
    {
        /// <summary>
        /// 取得指定角色於指定實驗室的權限位元向量與勾選清單
        /// </summary>
        Task<RoleLabPermissionResponseDto> GetRoleLabPermissionsAsync(Guid roleId, Guid labId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 更新指定角色於指定實驗室的權限位元向量
        /// </summary>
        Task<(bool Succeeded, string Message)> UpdateRoleLabPermissionsAsync(Guid roleId, Guid labId, UpdateRolePermissionsRequestDto request, CancellationToken cancellationToken = default);
    }
}