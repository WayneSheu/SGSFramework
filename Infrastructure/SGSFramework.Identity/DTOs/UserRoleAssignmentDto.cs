using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SGSFramework.Identity.DTOs
{
    /// <summary>
    /// 使用者角色編輯情境專用 DTO (聚合使用者資訊與全系統角色清單狀態)
    /// </summary>
    public record UserRoleAssignmentDto
    {
        /// <summary>
        /// 使用者唯一識別碼
        /// </summary>
        [JsonPropertyName("userId")]
        public string UserId { get; init; } = string.Empty;

        /// <summary>
        /// 使用者帳號名稱
        /// </summary>
        [JsonPropertyName("username")]
        public string Username { get; init; } = string.Empty;

        /// <summary>
        /// 全系統角色列表 (包含已繫結與未繫結角色)
        /// </summary>
        [JsonPropertyName("roles")]
        public List<RoleSelectionItemDto> Roles { get; init; } = [];
    }
}
