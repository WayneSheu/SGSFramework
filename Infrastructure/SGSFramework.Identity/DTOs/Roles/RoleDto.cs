using SGSFramework.Core.Abstractions.Entities.Identities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs.Roles
{
    public class RoleDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSystemRole { get; set; }

        public RoleScopeType ScopeType { get; set; }
        public string ScopeTypeName { get; set;} = string.Empty;

        /// <summary>
        /// 
        /// </summary>
        public int HierarchyLevel { get; set;}

        public Guid?  TenantLabId { get; set; }

        /// <summary>
        /// 狀態顯示文字（對應 Enum 的 DisplayName 或 Description）
        /// </summary>
        public string Status { get; set; } = string.Empty;

        public bool IsDeleted { get; set; }
        public List<string> MappedAdGroups { get; set; } = [];
        public List<RoleMemberItemDto> Members { get; set; } = [];

        /// <summary>
        /// 參考 PermissionController 結構之角色權限明細清單（包含 ModuleTitle、FunctionTitle、PermissionTitle 與描述）
        /// </summary>
        public List<RolePermissionItemDto> Permissions { get; set; } = [];
    }
}
