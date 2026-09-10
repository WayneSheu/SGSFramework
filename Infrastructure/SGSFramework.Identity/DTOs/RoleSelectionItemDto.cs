using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SGSFramework.Identity.DTOs
{
    /// <summary>
    /// 單一角色選擇狀態 DTO
    /// </summary>
    public record RoleSelectionItemDto
    {
        /// <summary>
        /// 角色唯一識別碼
        /// </summary>
        [JsonPropertyName("roleId")]
        public string RoleId { get; init; } = string.Empty;

        /// <summary>
        /// 角色名稱
        /// </summary>
        [JsonPropertyName("roleName")]
        public string RoleName { get; init; } = string.Empty;

        /// <summary>
        /// 角色描述說明
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; init; }

        /// <summary>
        /// 該使用者目前是否已繫結此角色
        /// </summary>
        [JsonPropertyName("isAssigned")]
        public bool IsAssigned { get; init; }
    }
}
