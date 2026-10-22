using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.PermissionAudits
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

        /// <summary>角色狀態（啟用中 / 已停用 / 已鎖定 / 待審核 / 已刪除）</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>是否計入繼承權限（與執行期授權規則一致）</summary>
        public bool IsEffective { get; set; } = true;

        /// <summary>不計入繼承權限的原因</summary>
        public string? IneffectiveReason { get; set; }

        /// <summary>是否有待審核的權限變更（繼承權限為核准前的既有權限）</summary>
        public bool HasPendingPermissionChange { get; set; }

        /// <summary>實驗室專屬角色適用的區域實驗室（全域 / 系統角色為空）</summary>
        public List<Guid> ScopeLabIds { get; set; } = [];

        public List<string> ScopeLabNames { get; set; } = [];
    }
}
