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
    Task<Result<List<UserDto>>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<Result<PagedResult<UserResponse>>> GetPagedUsersAsync(UserQueryParameters queryParameters, CancellationToken cancellationToken = default);
    Task<Result<UserResponse>> GetUserByIdAsync(TKey userId, CancellationToken cancellationToken = default);
    Task<Result<UserRoleAssignmentDto>> GetUserRoleAssignmentAsync(TKey userId, CancellationToken cancellationToken = default);
    Task<Result<TKey>> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task<Result<string>> RegisterAsync(CreateUserRequest request, string clientIp = "127.0.0.1", CancellationToken cancellationToken = default);
    Task<Result<bool>> ConfirmEmailAsync(TKey userId, string token, CancellationToken cancellationToken = default);
    Task<Result<ForgotPasswordResponse>> ForgotPasswordAsync(ForgotPasswordRequest request, string clientIp = "127.0.0.1", CancellationToken cancellationToken = default);
    Task<Result<bool>> UpdateUserAsync(TKey userId, UpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<Result<bool>> ResetPasswordAsync(TKey userId, ResetPasswordRequest request, string clientIp = "127.0.0.1", CancellationToken cancellationToken = default);
    Task<Result<bool>> ToggleUserStatusAsync(TKey userId, bool isActive, CancellationToken cancellationToken = default);
    Task<Result<bool>> DeleteUserAsync(TKey userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// 預設的使用者管理服務介面
/// </summary>
public interface IUserManagementService : IUserManagementService<Microsoft.AspNetCore.Identity.IdentityUser<System.Guid>, Microsoft.AspNetCore.Identity.IdentityRole<System.Guid>, System.Guid>
{
}