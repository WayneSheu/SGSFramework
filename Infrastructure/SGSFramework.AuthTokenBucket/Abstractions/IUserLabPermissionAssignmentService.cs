using SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.UserLabPermissions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Abstractions
{
    /// <summary>
    /// 針對主區域與兼任實驗室的差異化指派需求，設計具備異動檢核與防呆機制的應用服務
    /// </summary>
    public interface IUserLabPermissionAssignmentService
    {
        Task AssignOrUpdateLabPermissionAsync(
            Guid userId,
            int labId,
            string controllerOrModuleKey,
            long bitmask,
            string operatorId,
            CancellationToken cancellationToken = default);

        Task<List<UserLabPermissionDto>> GetLabPermissionsAsync(
            Guid userId,
            int labId,
            CancellationToken cancellationToken = default);
    }
}
