using SGSFramework.AuthTokenBucket.DTOs;
using SGSFramework.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Abstractions
{
    /// <summary>
    /// 執行期使用者作用域與動態權限管理服務介面
    /// </summary>
    public interface IUserRuntimeScopeService
    {
        Task<UserPermissionProfileDto> InitializeUserScopeAsync(
            string userId,
            string? requestedLabId,
            CancellationToken cancellationToken = default);

        Task<Guid?> GetPrimaryLabIdAsync(
            string userId,
            CancellationToken cancellationToken = default);

        Task<SwitchLabResultDto> SwitchLaboratoryWithFallbackAsync(
            string userId,
            Guid? targetLabId,
            CancellationToken cancellationToken = default);

        Task<UserPermissionProfileDto?> SwitchLaboratoryAsync(
            string userId,
            Guid targetLabId,
            CancellationToken cancellationToken = default);

        Task<IEnumerable<string>> GetUserPermissionsAsync(
            string userId,
            Guid? activeLabId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 驗證使用者於特定實驗室下的 Controller 與動態 BitPosition 權限點（支援超過 64 位元）
        /// </summary>
        Task<bool> ValidateRuntimePermissionAsync(
            string userId,
            Guid activeLabId,
            Guid controllerId,
            int bitPosition,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 透過模組代碼與動態 BitPosition 驗證執行期權限（支援超過 64 位元）
        /// </summary>
        Task<bool> ValidateRuntimePermissionAsync(
            string userId,
            Guid activeLabId,
            string module,
            int bitPosition,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 取得使用者可存取的實驗室清單
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<List<AccessibleLabDto>> GetAccessibleLabsAsync(
            string userId,
            CancellationToken cancellationToken = default);
    }
}
