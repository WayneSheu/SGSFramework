// ==========================================
// 檔案路徑: src/Infrastructure/SGSFramework.Identity/Services/UserManagementService.cs
// 架構層級: Infrastructure / Service Implementation Layer (Added AssignUserRolesAsync)
// ==========================================

#nullable enable

namespace SGSFramework.Identity.Services;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.AuthTokenBucket.Services;
using SGSFramework.Core.Abstractions.Entities.Base;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Logings;
using SGSFramework.Core.Abstractions.Transactions;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Paginations;
using SGSFramework.Core.Results;
using SGSFramework.Identity.Abstractions;
using SGSFramework.Identity.DTOs;
using SGSFramework.Identity.DTOs.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 企業級泛型使用者管理服務實作
/// </summary>
public class UserManagementService<TUser, TRole, TKey> : IUserManagementService<TUser, TRole, TKey>
    where TUser : ApplicationUser, IBaseUser, new()
    where TRole : IdentityRole<TKey>, IRoleEntity, new()
    where TKey : IEquatable<TKey>
{
    private readonly UserManager<TUser> _userManager;
    private readonly RoleManager<TRole> _roleManager;
    private readonly ILogger<UserManagementService<TUser, TRole, TKey>> _logger;
    private readonly TokenBucketEngine<TUser> _tokenEngine;
    private readonly ISecurityLogger _securityLogger;
    private readonly IUnitOfWork _unitOfWork;

    public UserManagementService(
        UserManager<TUser> userManager,
        RoleManager<TRole> roleManager,
        ILogger<UserManagementService<TUser, TRole, TKey>> logger,
        TokenBucketEngine<TUser> tokenEngine,
        ISecurityLogger securityLogger,
        IUnitOfWork unitOfWork)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tokenEngine = tokenEngine ?? throw new ArgumentNullException(nameof(tokenEngine));
        _securityLogger = securityLogger ?? throw new ArgumentNullException(nameof(securityLogger));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    /// <inheritdoc />
    public async Task<Result<List<UserDto>>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var users = await _userManager.Users
                .AsNoTracking()
                .Where(u => !u.IsDeleted)
                .OrderBy(u => u.UserName)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var userDtos = new List<UserDto>(users.Count);

            foreach (var user in users)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);

                userDtos.Add(new UserDto
                {
                    Id = user.Id.ToString() ?? string.Empty,
                    Username = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    EmailConfirmed = user.EmailConfirmed,
                    LockoutEnabled = user.LockoutEnabled,
                    Roles = roles.ToList()
                });
            }

            return Result.Success(userDtos);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[UserManagementService] 查詢使用者列表作業已被取消。");
            return Result.Failure<List<UserDto>>(Error.Validation("User.GetUsers.Cancelled", "查詢使用者列表作業已取消。"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserManagementService] 查詢使用者列表時發生系統異常。");
            return Result.Failure<List<UserDto>>(Error.Unexpected("User.GetUsers.Exception", "查詢使用者列表時發生內部系統錯誤。"));
        }
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<UserResponse>>> GetPagedUsersAsync(
        UserQueryParameters queryParameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queryParameters);

        try
        {
            var query = _userManager.Users.Where(u => !u.IsDeleted).AsNoTracking();

            if (!string.IsNullOrWhiteSpace(queryParameters.SearchTerm))
            {
                string searchTerm = queryParameters.SearchTerm.Trim();
                query = query.Where(u => (u.UserName != null && u.UserName.Contains(searchTerm)) ||
                                         (u.Email != null && u.Email.Contains(searchTerm)));
            }

            int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

            var userEntities = await query
                .OrderBy(u => u.UserName)
                .Skip((queryParameters.PageIndex - 1) * queryParameters.PageSize)
                .Take(queryParameters.PageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var items = userEntities.Select(u => new UserResponse
            {
                Id = u.Id is Guid gId ? gId : (Guid.TryParse(u.Id.ToString(), out var parsedId) ? parsedId : Guid.Empty),
                UserName = u.UserName ?? string.Empty,
                Email = u.Email ?? string.Empty,
                EmailConfirmed = u.EmailConfirmed
            }).ToList();

            var pagedResult = new PagedResult<UserResponse>(items, totalCount, queryParameters.PageIndex, queryParameters.PageSize);
            return Result.Success(pagedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "分頁查詢使用者清單時發生例外。");
            return Result.Failure<PagedResult<UserResponse>>(Error.Unexpected("User.GetPaged.Exception", "查詢使用者列表時發生內部系統錯誤。"));
        }
    }

    /// <inheritdoc />
    public async Task<Result<UserResponse>> GetUserByIdAsync(
        TKey userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString()!).ConfigureAwait(false);
            if (user == null || user.IsDeleted)
            {
                return Result.Failure<UserResponse>(Error.NotFound("User.NotFound", $"找不到識別碼為 {userId} 的使用者。"));
            }

            var response = new UserResponse
            {
                Id = user.Id is Guid gId ? gId : (Guid.TryParse(user.Id.ToString(), out var parsedId) ? parsedId : Guid.Empty),
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                EmailConfirmed = user.EmailConfirmed
            };

            return Result.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "依據 ID 取得使用者詳細資料時發生例外。UserId: {UserId}", userId);
            return Result.Failure<UserResponse>(Error.Unexpected("User.GetById.Exception", "查詢使用者資料時發生內部系統錯誤。"));
        }
    }

    /// <inheritdoc />
    public async Task<Result<UserRoleAssignmentDto>> GetUserRoleAssignmentAsync(
        TKey userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var user = await _userManager.FindByIdAsync(userId.ToString()!).ConfigureAwait(false);
            if (user == null || user.IsDeleted)
            {
                return Result.Failure<UserRoleAssignmentDto>(Error.NotFound("User.NotFound", $"找不到識別碼為 '{userId}' 的有效使用者。"));
            }

            var allRoles = await _roleManager.Roles.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);
            var userRoleNames = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
            var assignedSet = new HashSet<string>(userRoleNames, StringComparer.OrdinalIgnoreCase);

            var roleSelectionItems = allRoles.Select(r => new RoleSelectionItemDto
            {
                RoleId = r.Id?.ToString() ?? string.Empty,
                RoleName = r.Name ?? string.Empty,
                Description = r.Name,
                IsAssigned = r.Name != null && assignedSet.Contains(r.Name)
            }).ToList();

            var result = new UserRoleAssignmentDto
            {
                UserId = user.Id.ToString() ?? string.Empty,
                Username = user.UserName ?? string.Empty,
                Roles = roleSelectionItems
            };

            return Result.Success(result);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[UserManagementService] 查詢使用者角色狀態作業已被取消。UserId: {UserId}", userId);
            return Result.Failure<UserRoleAssignmentDto>(Error.Validation("User.GetRoleAssignment.Cancelled", "查詢使用者角色狀態作業已取消。"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserManagementService] 查詢使用者角色狀態時發生異常。UserId: {UserId}", userId);
            return Result.Failure<UserRoleAssignmentDto>(Error.Unexpected("User.GetRoleAssignment.Exception", "讀取使用者角色設定時發生內部系統錯誤。"));
        }
    }


    /// <inheritdoc />
    public async Task<Result<bool>> AssignUserRolesAsync(
        TKey userId,
        AssignUserRolesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (userId is Guid gId && gId == Guid.Empty)
            {
                return Result.Failure<bool>(Error.Validation("User.AssignRoles.InvalidId", "必須提供有效的使用者識別碼。"));
            }

            var user = await _userManager.FindByIdAsync(userId.ToString()!).ConfigureAwait(false);
            if (user == null || user.IsDeleted)
            {
                return Result.Failure<bool>(Error.NotFound("User.NotFound", $"找不到識別碼為 {userId} 的使用者。"));
            }

            var targetRoles = request.RoleNames ?? new List<string>();

            foreach (var roleName in targetRoles)
            {
                if (!await _roleManager.RoleExistsAsync(roleName).ConfigureAwait(false))
                {
                    return Result.Failure<bool>(Error.Validation("Role.NotFound", $"找不到指定的角色名稱: {roleName}"));
                }
            }

            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var currentRoles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);

                if (currentRoles.Count > 0)
                {
                    var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles).ConfigureAwait(false);
                    if (!removeResult.Succeeded)
                    {
                        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        string errors = string.Join("; ", removeResult.Errors.Select(e => e.Description));
                        return Result.Failure<bool>(Error.Validation("User.AssignRoles.RemoveFailed", $"移除舊角色失敗: {errors}"));
                    }
                }

                if (targetRoles.Count > 0)
                {
                    var addResult = await _userManager.AddToRolesAsync(user, targetRoles).ConfigureAwait(false);
                    if (!addResult.Succeeded)
                    {
                        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        string errors = string.Join("; ", addResult.Errors.Select(e => e.Description));
                        return Result.Failure<bool>(Error.Validation("User.AssignRoles.AddFailed", $"指派新角色失敗: {errors}"));
                    }
                }

                // 【關鍵修正】必須呼叫 SaveChangesAsync 將變更寫入資料庫，交易 Commit 才會生效
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("成功更新使用者角色指派。UserId: {UserId}, Roles: {Roles}", userId, string.Join(", ", targetRoles));
                return Result.Success(true);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[UserManagementService] 指派使用者角色作業已被取消。UserId: {UserId}", userId);
            return Result.Failure<bool>(Error.Validation("User.AssignRoles.Cancelled", "指派使用者角色作業已取消。"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserManagementService] 指派使用者角色時發生系統異常。UserId: {UserId}", userId);
            return Result.Failure<bool>(Error.Unexpected("User.AssignRoles.Exception", "指派使用者角色時發生內部系統錯誤。"));
        }
    }

    /// <inheritdoc />
    public async Task<Result<TKey>> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var user = new TUser
            {
                UserName = request.UserName,
                Email = request.Email,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, request.Password).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                string errors = string.Join("; ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("建立使用者失敗: {Errors}", errors);
                return Result.Failure<TKey>(Error.Validation("User.Create.Failed", $"建立使用者失敗: {errors}"));
            }

            TKey newUserId = (TKey)(object)user.Id;
            _logger.LogInformation("成功建立使用者: {UserId} ({UserName})", newUserId, request.UserName);
            return Result.Success(newUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "建立使用者時發生例外。UserName: {UserName}", request.UserName);
            return Result.Failure<TKey>(Error.Unexpected("User.Create.Exception", "建立使用者時發生內部系統錯誤。"));
        }
    }

    /// <inheritdoc />
    public async Task<Result<string>> RegisterAsync(
        CreateUserRequest request,
        string clientIp = "127.0.0.1",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var userByName = await _userManager.FindByNameAsync(request.UserName).ConfigureAwait(false);
            if (userByName != null)
            {
                return Result.Failure<string>(Error.Validation("User.Register.UsernameExists", "該帳號名稱已被使用。"));
            }

            var userByEmail = await _userManager.FindByEmailAsync(request.Email).ConfigureAwait(false);
            if (userByEmail != null)
            {
                return Result.Failure<string>(Error.Validation("User.Register.EmailExists", "該電子郵件已被註冊。"));
            }

            var user = new TUser
            {
                UserName = request.UserName,
                Email = request.Email,
                EmailConfirmed = false,
                LockoutEnabled = true
            };

            var result = await _userManager.CreateAsync(user, request.Password).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                string errors = string.Join("; ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("使用者註冊失敗: {Errors}", errors);
                return Result.Failure<string>(Error.Validation("User.Register.Failed", $"註冊失敗: {errors}"));
            }

            string emailToken = await _userManager.GenerateEmailConfirmationTokenAsync(user).ConfigureAwait(false);

            _securityLogger.LogSecurity(
                eventCode: "SEC-200-USER-REGISTER",
                eventCategory: "UserManagement.Register",
                userId: user.Id.ToString() ?? string.Empty,
                clientIp: clientIp,
                messageTemplate: "使用者註冊成功。帳號: {Username}, Email: {Email}",
                user.UserName ?? string.Empty,
                user.Email ?? string.Empty
            );

            return Result.Success(emailToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[UserManagementService] 註冊使用者作業已被取消。");
            return Result.Failure<string>(Error.Validation("User.Register.Cancelled", "註冊作業已取消。"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "註冊作業發生未預期異常。");
            return Result.Failure<string>(Error.Unexpected("User.Register.Exception", "註冊作業處理期間發生未預期錯誤。"));
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ConfirmEmailAsync(
        TKey userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        try
        {
            var user = await _userManager.FindByIdAsync(userId?.ToString() ?? string.Empty).ConfigureAwait(false);
            if (user == null || user.IsDeleted)
            {
                return Result.Failure<bool>(Error.NotFound("User.NotFound", $"找不到識別碼為 {userId} 的使用者。"));
            }

            if (user.EmailConfirmed)
            {
                return Result.Success(true);
            }

            var result = await _userManager.ConfirmEmailAsync(user, token).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                string errors = string.Join("; ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("確認電子郵件失敗 {UserId}: {Errors}", userId, errors);
                return Result.Failure<bool>(Error.Validation("User.ConfirmEmail.Failed", $"電子郵件驗證失敗: {errors}"));
            }

            _logger.LogInformation("成功確認使用者電子郵件: {UserId}", userId);
            return Result.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "確認電子郵件時發生例外。UserId: {UserId}", userId);
            return Result.Failure<bool>(Error.Unexpected("User.ConfirmEmail.Exception", "驗證電子郵件時發生內部系統錯誤。"));
        }
    }

    /// <inheritdoc />
    public async Task<Result<ForgotPasswordResponse>> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        string clientIp = "127.0.0.1",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var user = await _userManager.FindByEmailAsync(request.Email).ConfigureAwait(false);
            if (user == null || user.IsDeleted || !await _userManager.IsEmailConfirmedAsync(user).ConfigureAwait(false))
            {
                return Result.Success(new ForgotPasswordResponse
                {
                    Message = "若帳號存在且已完成啟用，重設密碼信件已發送至您的信箱。",
                    DebugResetToken = null
                });
            }

            string resetToken = await _userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);

            _securityLogger.LogSecurity(
                eventCode: "SEC-200-FORGOT-PASSWORD-REQUEST",
                eventCategory: "UserManagement.ForgotPassword",
                userId: user.Id.ToString() ?? string.Empty,
                clientIp: clientIp,
                messageTemplate: "使用者申請密碼重設憑證。Email: {Email}",
                request.Email
            );

            return Result.Success(new ForgotPasswordResponse
            {
                Message = "重設密碼信件已發送。",
                DebugResetToken = resetToken
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[UserManagementService] 忘記密碼作業已被取消。");
            return Result.Failure<ForgotPasswordResponse>(Error.Validation("User.ForgotPassword.Cancelled", "忘記密碼作業已取消。"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "忘記密碼服務發生未預期異常。Email: {Email}", request.Email);
            return Result.Failure<ForgotPasswordResponse>(Error.Unexpected("User.ForgotPassword.Exception", "發送重設密碼請求時發生內部系統錯誤。"));
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> UpdateUserAsync(
        TKey userId,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString()!).ConfigureAwait(false);
            if (user == null || user.IsDeleted)
            {
                return Result.Failure<bool>(Error.NotFound("User.NotFound", $"找不到識別碼為 {userId} 的使用者。"));
            }

            user.UserName = request.UserName;
            user.Email = request.Email;

            var result = await _userManager.UpdateAsync(user).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                string errors = string.Join("; ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("更新使用者失敗 {UserId}: {Errors}", userId, errors);
                return Result.Failure<bool>(Error.Validation("User.Update.Failed", $"更新使用者失敗: {errors}"));
            }

            _logger.LogInformation("成功更新使用者: {UserId}", userId);
            return Result.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "更新使用者時發生例外。UserId: {UserId}", userId);
            return Result.Failure<bool>(Error.Unexpected("User.Update.Exception", "更新使用者時發生內部系統錯誤。"));
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ResetPasswordAsync(
        TKey userId,
        ResetPasswordRequest request,
        string clientIp = "127.0.0.1",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (userId is Guid gId && gId == Guid.Empty)
            {
                return Result.Failure<bool>(Error.Validation("User.ResetPassword.InvalidId", "必須提供有效的使用者識別碼。"));
            }

            var user = await _userManager.FindByIdAsync(userId.ToString()!).ConfigureAwait(false);
            if (user == null || user.IsDeleted)
            {
                return Result.Failure<bool>(Error.NotFound("User.NotFound", $"找不到識別碼為 {userId} 的使用者。"));
            }

            string token = await _userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);
            var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                string errors = string.Join("; ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("重設密碼失敗 {UserId}: {Errors}", userId, errors);
                return Result.Failure<bool>(Error.Validation("User.ResetPassword.Failed", $"重設密碼失敗: {errors}"));
            }

            _securityLogger.LogSecurity(
                eventCode: "SEC-200-PASSWORD-RESET",
                eventCategory: "UserManagement.ResetPassword",
                userId: userId.ToString() ?? string.Empty,
                clientIp: clientIp,
                messageTemplate: "管理員重設使用者密碼成功。目標使用者: {TargetUserId}",
                userId.ToString() ?? string.Empty
            );

            _logger.LogInformation("成功重設使用者密碼: {UserId}", userId);
            return Result.Success(true);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[UserManagementService] 重設密碼作業已被取消。UserId: {UserId}", userId);
            return Result.Failure<bool>(Error.Validation("User.ResetPassword.Cancelled", "重設密碼作業已取消。"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重設密碼時發生例外。UserId: {UserId}", userId);
            return Result.Failure<bool>(Error.Unexpected("User.ResetPassword.Exception", "重設密碼時發生內部系統錯誤。"));
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ToggleUserStatusAsync(
        TKey userId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId.ToString()!).ConfigureAwait(false);
            if (user == null || user.IsDeleted)
            {
                return Result.Failure<bool>(Error.NotFound("User.NotFound", $"找不到識別碼為 {userId} 的使用者。"));
            }

            var lockoutEnd = isActive ? (DateTimeOffset?)null : DateTimeOffset.MaxValue;
            await _userManager.SetLockoutEnabledAsync(user, !isActive).ConfigureAwait(false);
            var result = await _userManager.SetLockoutEndDateAsync(user, lockoutEnd).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                return Result.Failure<bool>(Error.Validation("User.Status.Failed", "變更使用者狀態失敗。"));
            }

            _logger.LogInformation("成功變更使用者狀態: {UserId}, IsActive: {IsActive}", userId, isActive);
            return Result.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "變更使用者狀態時發生例外。UserId: {UserId}", userId);
            return Result.Failure<bool>(Error.Unexpected("User.Status.Exception", "變更使用者狀態時發生內部系統錯誤。"));
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> DeleteUserAsync(
        TKey userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string currentUserId = "sysadmin";
            string clientIp = "127.0.0.1";

            if (string.Equals(currentUserId, userId?.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<bool>(Error.Validation("User.Delete.Self", "系統禁止管理員執行刪除自身的帳號動作。"));
            }

            var user = await _userManager.FindByIdAsync(userId?.ToString() ?? string.Empty).ConfigureAwait(false);
            if (user == null || user.IsDeleted)
            {
                return Result.Failure<bool>(Error.NotFound("User.NotFound", $"找不到識別碼為 '{userId}' 的有效使用者。"));
            }

            if (user.IsSystemAdmin ||
                string.Equals(user.UserName, "admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(user.UserName, "superadmin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(user.UserName, "administrator", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(user.UserName, "superuser", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(user.UserName, "sys", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(user.UserName, "sysadmin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(user.UserName, "system", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(user.UserName, "systemadmin", StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<bool>(Error.Validation("User.Delete.SystemAccountRestricted", "系統內建核心帳號受到保護，禁止刪除。"));
            }

            string originalUserName = user.UserName ?? string.Empty;
            string originalEmail = user.Email ?? string.Empty;

            await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string userIdStr = user.Id.ToString() ?? string.Empty;
                string anonymizedTag = userIdStr.Length >= 8 ? userIdStr[..8] : userIdStr;

                user.IsDeleted = true;
                user.DeletedAt = DateTimeOffset.UtcNow;
                user.DeletedBy = currentUserId;

                user.UserName = $"deleted_user_{anonymizedTag}";
                user.NormalizedUserName = $"DELETED_USER_{anonymizedTag.ToUpperInvariant()}";
                user.Email = $"deleted_{anonymizedTag}@anonymized.local";
                user.NormalizedEmail = $"DELETED_{anonymizedTag.ToUpperInvariant()}@ANONYMIZED.LOCAL";
                user.PhoneNumber = null;
                user.EmailConfirmed = false;
                user.PhoneNumberConfirmed = false;
                user.TwoFactorEnabled = false;
                user.LockoutEnd = DateTimeOffset.MaxValue;

                var currentRoles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
                if (currentRoles.Count > 0)
                {
                    var removeRoleResult = await _userManager.RemoveFromRolesAsync(user, currentRoles).ConfigureAwait(false);
                    if (!removeRoleResult.Succeeded)
                    {
                        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                        string roleErrors = string.Join("; ", removeRoleResult.Errors.Select(e => e.Description));
                        return Result.Failure<bool>(Error.Validation("User.Delete.RemoveRoleFailed", $"刪除使用者失敗: {roleErrors}"));
                    }
                }

                var updateResult = await _userManager.UpdateAsync(user).ConfigureAwait(false);
                if (!updateResult.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    string updateErrors = string.Join("; ", updateResult.Errors.Select(e => e.Description));
                    return Result.Failure<bool>(Error.Validation("User.Delete.UpdateFailed", $"刪除使用者失敗: {updateErrors}"));
                }

                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                await _tokenEngine.EmergencyFreezeAsync(userIdStr, "使用者帳號已執行軟刪除與個資抹除，全面作廢憑證。").ConfigureAwait(false);
                await _tokenEngine.CompleteRemediationAsync(userIdStr).ConfigureAwait(false);

                _securityLogger.LogSecurity(
                    eventCode: "SEC-200-USER-DELETED",
                    eventCategory: "UserManagement.DeleteUser",
                    userId: currentUserId,
                    clientIp: clientIp,
                    messageTemplate: "管理員 [{AdminId}] 軟刪除並匿名化使用者帳號。目標識別碼: {TargetUserId}, 原帳號: {OriginalUserName}, 原 Email: {OriginalEmail}",
                    currentUserId,
                    userIdStr,
                    originalUserName,
                    originalEmail
                );

                return Result.Success(true);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[UserManagementService] 軟刪除使用者作業已被取消。UserId: {UserId}", userId);
            return Result.Failure<bool>(Error.Validation("User.Delete.Cancelled", "刪除使用者作業已取消。"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[UserManagementService] 軟刪除使用者時發生系統異常。UserId: {UserId}", userId);
            return Result.Failure<bool>(Error.Unexpected("User.Delete.Exception", "執行刪除使用者作業時發生系統異常。"));
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool>> ChangePasswordAsync(
        TKey userId,
        ChangePasswordRequest request,
        string clientIp = "127.0.0.1",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (userId is Guid gId && gId == Guid.Empty)
            {
                return Result.Failure<bool>(Error.Validation("User.ChangePassword.InvalidId", "必須提供有效的使用者識別碼。"));
            }

            var user = await _userManager.FindByIdAsync(userId.ToString()!).ConfigureAwait(false);
            if (user == null || user.IsDeleted)
            {
                return Result.Failure<bool>(Error.NotFound("User.NotFound", $"找不到識別碼為 {userId} 的使用者。"));
            }

            var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                string errors = string.Join("; ", result.Errors.Select(e => e.Description));

                _securityLogger.LogSecurity(
                    eventCode: "SEC-400-PASSWORD-CHANGE-FAILED",
                    eventCategory: "UserManagement.ChangePassword",
                    userId: userId.ToString() ?? string.Empty,
                    clientIp: clientIp,
                    messageTemplate: "使用者線上變更密碼失敗。用戶識別碼: {UserId}, 原因: {Errors}",
                    userId.ToString() ?? string.Empty,
                    errors
                );

                return Result.Failure<bool>(Error.Validation("User.ChangePassword.Failed", $"變更密碼失敗: {errors}"));
            }

            await _tokenEngine.EmergencyFreezeAsync(user.Id.ToString() ?? string.Empty, "使用者執行線上變更密碼，強制登出全網所有裝置工作階段。").ConfigureAwait(false);
            await _tokenEngine.CompleteRemediationAsync(user.Id.ToString() ?? string.Empty).ConfigureAwait(false);

            _securityLogger.LogSecurity(
                eventCode: "SEC-200-PASSWORD-CHANGED",
                eventCategory: "UserManagement.ChangePassword",
                userId: userId.ToString() ?? string.Empty,
                clientIp: clientIp,
                messageTemplate: "使用者線上變更密碼成功，並已肅清全網 Session。帳號: {Username}",
                user.UserName ?? string.Empty
            );

            _logger.LogInformation("使用者成功變更密碼並重置 Session: {UserId}", userId);
            return Result.Success(true);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("[UserManagementService] 變更密碼作業已被取消。UserId: {UserId}", userId);
            return Result.Failure<bool>(Error.Validation("User.ChangePassword.Cancelled", "變更密碼作業已取消。"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "變更密碼時發生例外。UserId: {UserId}", userId);
            return Result.Failure<bool>(Error.Unexpected("User.ChangePassword.Exception", "變更密碼時發生內部系統錯誤。"));
        }
    }
}

/// <summary>
/// 相容具體型別的服務實作包裝
/// </summary>
public sealed class UserManagementService : UserManagementService<ApplicationUser, ApplicationRole, Guid>, IUserManagementService
{
    public UserManagementService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        ILogger<UserManagementService<ApplicationUser, ApplicationRole, Guid>> logger,
        TokenBucketEngine<ApplicationUser> tokenEngine,
        ISecurityLogger securityLogger,
        IUnitOfWork unitOfWork)
        : base(userManager, roleManager, logger, tokenEngine, securityLogger, unitOfWork)
    {
    }
}