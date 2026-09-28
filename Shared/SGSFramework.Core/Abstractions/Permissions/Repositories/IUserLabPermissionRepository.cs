#nullable enable

using SGSFramework;
using SGSFramework.Core.Abstractions.Permissions.Identities;

namespace SGSFramework.Core.Abstractions.Permissions.Repositories;

public interface IUserLabPermissionRepository
{
    Task<UserLabPermission?> GetPermissionAsync(Guid userId, int labId, string controllerOrModuleKey, CancellationToken cancellationToken = default);
    Task AddAsync(UserLabPermission permission, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserLabPermission permission, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}