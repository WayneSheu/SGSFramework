using SGSFramework.AuthTokenBucket.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Abstractions
{
    public interface IPermissionManagementService
    {
        Task<List<PermissionModuleDto>> GetPermissionTreeAsync(CancellationToken cancellationToken = default);
        Task<RolePermissionMatrixDto?> GetRolePermissionsAsync(string roleId, CancellationToken cancellationToken = default);
        Task<bool> UpdateRolePermissionsAsync(UpdateRolePermissionsRequest request, CancellationToken cancellationToken = default);
    }
}
