using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs.RolePermissions
{

    /// <summary>
    /// 更新角色權限請求 DTO
    /// </summary>
    public sealed record UpdateRoleGlobalPermissionsRequest
    {
        /// <summary>
        /// 角色識別碼
        /// </summary>
        [Required(ErrorMessage = "角色識別碼為必填項。")]
        public string RoleId { get; init; } = string.Empty;

        /// <summary>
        /// 欲指派之權限代碼清單
        /// </summary>
        [Required(ErrorMessage = "權限清單不可為 null。")]
        public List<string> PermissionKeys { get; init; } = [];
    }
}
