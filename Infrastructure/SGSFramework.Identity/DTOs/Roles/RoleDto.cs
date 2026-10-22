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

        /// <summary>稽核角色：權限僅於具有效稽核指派的區域實驗室生效</summary>
        public bool RequiresAuditAssignment { get; set; }

        public RoleScopeType ScopeType { get; set; }
        public string ScopeTypeName { get; set;} = string.Empty;

        /// <summary>
        /// 
        /// </summary>
        public int HierarchyLevel { get; set;}

        public Guid?  TenantLabId { get; set; }

        /// <summary>實驗室專屬角色適用的區域實驗室</summary>
        public List<Guid> TenantLabIds { get; set; } = [];
        public DateTimeOffset? EffectiveFrom { get; set; }
        public DateTimeOffset? EffectiveTo { get; set; }

        /// <summary>
        /// 狀態顯示文字（對應 Enum 的 DisplayName 或 Description）
        /// </summary>
        public string Status { get; set; } = string.Empty;
        /// <summary>狀態數值 (RoleStatus)：0 已停用、1 啟用中、2 已鎖定、3 待審核，供前端切換狀態使用</summary>
        public int StatusValue { get; set; }

        /// <summary>是否有待審核的權限變更申請（此時狀態為「待審核」，須至權限變更審核處理）</summary>
        public bool HasPendingPermissionChange { get; set; }

        public bool IsDeleted { get; set; }
        public List<string> MappedAdGroups { get; set; } = [];
        public List<RoleMemberItemDto> Members { get; set; } = [];

        /// <summary>
        /// 參考 PermissionController 結構之角色權限明細清單（包含 ModuleTitle、FunctionTitle、PermissionTitle 與描述）
        /// </summary>
        public List<RolePermissionItemDto> Permissions { get; set; } = [];
    }
}
