using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs.PermissionAudits
{
    /// <summary>
    /// 角色稽核資訊 DTO
    /// </summary>
    public sealed class RoleAuditInfoItemDto
    {
        /// <summary>
        /// 角色名稱
        /// </summary>
        public string RoleName { get; set; } = string.Empty;

        /// <summary>
        /// 角色描述
        /// </summary>
        public string? Description { get; set; }
    }
}
