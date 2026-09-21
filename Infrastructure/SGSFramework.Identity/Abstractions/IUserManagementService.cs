// ==========================================
// 檔案路徑: src/Core/SGSFramework.Identity.Abstractions/IUserManagementService.cs
// 架構層級: Domain / Application Abstractions Layer
// ==========================================

#nullable enable

namespace SGSFramework.Identity.Abstractions;

using SGSFramework.Core.Paginations;
using SGSFramework.Core.Results;
using SGSFramework.Identity.DTOs;
using SGSFramework.Identity.DTOs.Users;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 企業級使用者管理服務介面
/// </summary>
public interface IUserManagementService<TUser, TRole, TKey>
    where TKey : System.IEquatable<TKey>
{
    /// <summary>
    /// 取得所有使用者列表
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Result<List<UserDto>>> GetUsersAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 取得分頁使用者列表
    /// </summary>
    /// <param name="queryParameters"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Result<PagedResult<UserResponse>>> GetPagedUsersAsync(UserQueryParameters queryParameters, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 取得使用者資訊
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Result<UserResponse>> GetUserByIdAsync(TKey userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 取得使用者角色分配資訊
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Result<UserRoleAssignmentDto>> GetUserRoleAssignmentAsync(TKey userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 分配使用者角色
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Result<bool>> AssignUserRolesAsync(
        TKey userId,
        AssignUserRolesRequest request,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// 創建新使用者
    /// </summary>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Result<TKey>> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 更新使用者資訊
    /// </summary>
    /// <param name="request"></param>
    /// <param name="clientIp"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Result<string>> RegisterAsync(CreateUserRequest request, string clientIp = "127.0.0.1", CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 驗證使用者電子郵件地址
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="token"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Result<bool>> ConfirmEmailAsync(TKey userId, string token, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 忘記密碼並發送電子郵件通知
    /// </summary>
    /// <param name="request"></param>
    /// <param name="clientIp"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Result<ForgotPasswordResponse>> ForgotPasswordAsync(ForgotPasswordRequest request, string clientIp = "127.0.0.1", CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 更新使用者資訊
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="request"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Result<bool>> UpdateUserAsync(TKey userId, UpdateUserRequest request, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 重置使用者密碼
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="request"></param>
    /// <param name="clientIp"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Result<bool>> ResetPasswordAsync(TKey userId, ResetPasswordRequest request, string clientIp = "127.0.0.1", CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 啟用或禁用使用者狀態
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="isActive"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Result<bool>> ToggleUserStatusAsync(TKey userId, bool isActive, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 刪除使用者
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Result<bool>> DeleteUserAsync(TKey userId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 變更使用者密碼並執行資安聯防工作階段清除
    /// </summary>
    /// <param name="userId">使用者識別碼</param>
    /// <param name="request">變更密碼請求內容</param>
    /// <param name="clientIp">用戶端 IP 位址</param>
    /// <param name="cancellationToken">取消權杖</param>
    /// <returns>是否成功</returns>
    Task<Result<bool>> ChangePasswordAsync(
        TKey userId,
        ChangePasswordRequest request,
        string clientIp = "127.0.0.1",
        CancellationToken cancellationToken = default);

}

/// <summary>
/// 預設的使用者管理服務介面
/// </summary>
public interface IUserManagementService : IUserManagementService<Microsoft.AspNetCore.Identity.IdentityUser<System.Guid>, Microsoft.AspNetCore.Identity.IdentityRole<System.Guid>, System.Guid>
{
}