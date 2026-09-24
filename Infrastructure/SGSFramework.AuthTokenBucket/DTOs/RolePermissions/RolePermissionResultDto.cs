using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.DTOs.RolePermissions
{
    /// <summary>
    /// 角色權限異動結果 DTO
    /// </summary>
    public sealed record RolePermissionResultDto
    {
        public string RoleId { get; init; } = string.Empty;
        public int UpdatedCount { get; init; }
        public List<string> AssignedPermissions { get; init; } = [];
        public DateTime ProcessedAtUtc { get; init; } = DateTime.UtcNow;
    }
}
