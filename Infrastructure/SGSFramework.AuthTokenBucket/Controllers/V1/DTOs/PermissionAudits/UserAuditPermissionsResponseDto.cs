using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.PermissionAudits
{
    /// <summary>
    /// 完整使用者權限稽核 DTO (企業級合規架構)
    /// </summary>
    public sealed class UserAuditPermissionsResponseDto
    {
        /// <summary>
        /// 使用者識別碼
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 使用者帳號
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 全域角色清單 (包含角色描述)
        /// </summary>
        public List<RoleAuditInfoItemDto> Roles { get; set; } = new();

        /// <summary>
        /// 全域直接指派權限 (Global Direct Granted)
        /// </summary>
        public List<PermissionAuditInfoItemDto> GlobalDirectPermissions { get; set; } = new();

        /// <summary>
        /// 全域繼承角色權限 (Global Role Inherited)
        /// </summary>
        public List<PermissionAuditInfoItemDto> GlobalRolePermissions { get; set; } = new();

        /// <summary>
        /// 全域有效權限 (Global Direct ∪ Global Role)
        /// </summary>
        public List<PermissionAuditInfoItemDto> GlobalEffectivePermissions { get; set; } = new();

        /// <summary>
        /// 主區域實驗室權限資訊 (Primary Lab)
        /// </summary>
        public UserLabPermissionAuditDto? PrimaryLabPermission { get; set; }

        /// <summary>
        /// 兼區域實驗室權限資訊清單 (Secondary / Concurrent Labs)
        /// </summary>
        public List<UserLabPermissionAuditDto> SecondaryLabPermissions { get; set; } = new();
    }
}
