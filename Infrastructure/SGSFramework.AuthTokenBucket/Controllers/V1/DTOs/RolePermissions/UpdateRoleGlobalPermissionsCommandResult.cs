using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.RolePermissions
{
    /// <summary>
    /// 角色全域權限更新結果
    /// </summary>
    public sealed record UpdateRoleGlobalPermissionsCommandResult(
        bool Succeeded,
        string Message,
        int AffectedModulesCount
    );
}
