// 檔案路徑: Domain/SGSFramework.Identity.Abstractions/IRoleManagementService.cs

using Microsoft.AspNetCore.Identity;
using SGSFramework.Identity.DTOs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SGSFramework.Identity.Abstractions
{
    /// <summary>
    /// 企業級泛型角色管理與 AD Group 整合服務介面
    /// </summary>
    /// <typeparam name="TRole">角色實體型別，必須繼承自 IdentityRole&lt;TKey&gt;</typeparam>
    /// <typeparam name="TKey">主鍵型別，必須實作 IEquatable&lt;TKey&gt;</typeparam>
    public interface IRoleManagementService<TRole, TKey>
        where TRole : IdentityRole<TKey>, new() // 必須加上與實作類別相同的條件約束
        where TKey : IEquatable<TKey>
    {
        Task<List<RoleDto>> GetAllRolesAsync(CancellationToken cancellationToken = default);
        Task<RoleDto?> GetRoleByIdAsync(string roleId, CancellationToken cancellationToken = default);
        Task<(bool Succeeded, IEnumerable<string> Errors)> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);
        Task<(bool Succeeded, IEnumerable<string> Errors)> UpdateRoleAsync(UpdateRoleRequest request, CancellationToken cancellationToken = default);
        Task<(bool Succeeded, IEnumerable<string> Errors)> DeleteRoleAsync(string roleId, CancellationToken cancellationToken = default);

        // AD Group 對應服務介面
        Task<(bool Succeeded, string Message)> MapAdGroupToRoleAsync(MapAdGroupToRoleRequest request, CancellationToken cancellationToken = default);
        Task<(bool Succeeded, string Message)> RemoveAdGroupFromRoleAsync(RemoveAdGroupFromRoleRequest request, CancellationToken cancellationToken = default);
        Task<(bool Succeeded, List<string> SyncedRoles, string Message)> SyncUserRolesFromAdGroupsAsync(SyncUserAdRolesRequest request, CancellationToken cancellationToken = default);

        // 使用者角色綁定服務介面
        Task<(bool Succeeded, string Message)> AssignUserRolesAsync(AssignUserRolesRequest request, CancellationToken cancellationToken = default);

        // 批次使用者角色解綁服務介面
        Task<(bool Succeeded, string Message, IEnumerable<string>? Errors)> BatchAssignUsersToRoleAsync(
          string roleIdentifier,
          BatchAssignUsersRequest request,
          CancellationToken cancellationToken = default);
    }
}