using System;
using System.Collections.Generic;
using System.Text;

namespace GSFramework.AuthTokenBucket.DTOs
{
    /// <summary>
    /// 稽核使用者權限 DTO
    /// </summary>
    public sealed class UserAuditPermissionsResponseDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 角色 DTO
        /// </summary>
        public List<string> Roles { get; set; } = new();

        /// <summary>
        /// 直接權限 DTO
        /// </summary>
        public List<string> DirectPermissions { get; set; } = new();

        /// <summary>
        /// 有效權限 DTO
        /// </summary>
        public List<string> EffectivePermissions { get; set; } = new();
    }
}
