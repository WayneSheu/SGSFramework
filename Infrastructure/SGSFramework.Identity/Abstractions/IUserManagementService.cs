using SGSFramework.Core.Paginations;
using SGSFramework.Core.Results;
using SGSFramework.Identity.DTOs.Users;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.Abstractions
{
    /// <summary> 
    /// 使用者管理應用層服務介面 
    /// </summary> 
    public interface IUserManagementService
    {
        /// <summary> 
        /// 分頁查詢使用者列表 
        /// </summary> 
        Task<Result<PagedResult<UserResponse>>> GetPagedUsersAsync(
            UserQueryParameters queryParameters,
            CancellationToken cancellationToken = default);

        /// <summary> 
        /// 依據 ID 取得使用者詳細資料 
        /// </summary> 
        Task<Result<UserResponse>> GetUserByIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary> 
        /// 建立全新使用者並配置實驗室維度與權限向量 
        /// </summary> 
        Task<Result<Guid>> CreateUserAsync(
            CreateUserRequest request,
            CancellationToken cancellationToken = default);

        /// <summary> 
        /// 更新使用者基本資料與角色配置 
        /// </summary> 
        Task<Result<bool>> UpdateUserAsync(
            Guid userId,
            UpdateUserRequest request,
            CancellationToken cancellationToken = default);

        /// <summary> 
        /// 管理者強制重設使用者密碼 
        /// </summary> 
        Task<Result<bool>> ResetPasswordAsync(
            Guid userId,
            ResetPasswordRequest request,
            CancellationToken cancellationToken = default);

        /// <summary> 
        /// 切換使用者啟用/停用狀態 
        /// </summary> 
        Task<Result<bool>> ToggleUserStatusAsync(
            Guid userId,
            bool isActive,
            CancellationToken cancellationToken = default);

        /// <summary> 
        /// 刪除指定使用者 
        /// </summary> 
        Task<Result<bool>> DeleteUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default);
    }
}
