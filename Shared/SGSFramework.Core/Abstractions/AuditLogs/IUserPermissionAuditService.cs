using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.AuditLogs
{
    /// <summary>
    /// 權限稽核服務接口
    /// </summary>
    public interface IUserPermissionAuditService
    {
        Task AuditPermissionChangeAsync(
            string operatorId,
            string targetUserId,
            string permissionKey,
            long oldBitmask,
            long newBitmask,
            CancellationToken cancellationToken = default);
    }
}
