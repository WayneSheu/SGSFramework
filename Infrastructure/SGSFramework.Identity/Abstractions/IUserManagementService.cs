// ==========================================
// 檔案路徑: src/Core/SGSFramework.Identity.Abstractions/IUserManagementService.cs
// 架構層級: Application / Abstractions Layer
// ==========================================

#nullable enable

namespace SGSFramework.Identity.Abstractions;

using Microsoft.AspNetCore.Identity;
using SGSFramework.Core.Abstractions.Entities.Base;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Paginations;
using SGSFramework.Core.Results;
using SGSFramework.Identity.DTOs;
using SGSFramework.Identity.DTOs.Users;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 使用者管理應用層服務泛型介面
/// </summary>
public interface IUserManagementService<TUser, TRole, TKey>
    where TUser : ApplicationUser, IBaseUser, new()
    where TRole : IdentityRole<TKey>, IRoleEntity, new()
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// 查詢所有使用者列表
    /// </summary>
    Task<Result<List<UserDto>>> GetUsersAsync(CancellationToken cancellationToken = default);

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
        TKey userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用者帳號註冊並生成電子郵件驗證憑證。
    /// </summary>
    /// <param name="request"></param>
    /// <param name="clientIp"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Result<string>> RegisterAsync(
    CreateUserRequest request,
    string clientIp = "127.0.0.1",// 預設值為 "127.0.0.1"，表示本地機器 IP 地址
    CancellationToken cancellationToken = default);

    /// <summary>
    /// 建立全新使用者並配置實驗室維度與權限向量
    /// </summary>
    Task<Result<TKey>> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新使用者基本資料與角色配置
    /// </summary>
    Task<Result<bool>> UpdateUserAsync(
        TKey userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 管理者強制重設使用者密碼
    /// </summary>
    Task<Result<bool>> ResetPasswordAsync(
        TKey userId,
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 切換使用者啟用/停用狀態
    /// </summary>
    Task<Result<bool>> ToggleUserStatusAsync(
        TKey userId,
        bool isActive,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 刪除指定使用者
    /// </summary>
    Task<Result<bool>> DeleteUserAsync(
        TKey userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 根據使用者 ID 取得角色分配詳細資料
    /// </summary>
    Task<Result<UserRoleAssignmentDto>> GetUserRoleAssignmentAsync(
        TKey userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 確認使用者電子郵件
    /// </summary>
    Task<Result<bool>> ConfirmEmailAsync(TKey userId, string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// 忘記密碼並發送電子郵件驗證憑證
    /// </summary>
    /// <param name="request"></param>
    /// <param name="clientIp"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Result<ForgotPasswordResponse>> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        string clientIp = "127.0.0.1",
        CancellationToken cancellationToken = default);
}

/// <summary>
/// 相容具體型別的非泛型介面包裝（需使用實作 IRoleEntity 的具體角色類別，例如 ApplicationRole）
/// </summary>
public interface IUserManagementService : IUserManagementService<ApplicationUser, ApplicationRole, Guid>
{
}