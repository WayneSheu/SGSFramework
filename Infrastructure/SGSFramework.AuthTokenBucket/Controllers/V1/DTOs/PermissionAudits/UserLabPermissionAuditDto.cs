using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.AuthTokenBucket.Controllers.V1.DTOs.PermissionAudits
{
    /// <summary>
    /// 實驗室層級權限稽核 DTO (包含生效/失效時間與區域權限來源)
    /// </summary>
    public sealed class UserLabPermissionAuditDto
    {
        /// <summary>
        /// 租戶實驗室識別碼
        /// </summary>
        public Guid TenantLabId { get; set; }

        /// <summary>
        /// 上級區域/類別代碼
        /// </summary>
        public string CategoryCode { get; set; } = string.Empty;

        /// <summary>
        /// 上級區域/類別名稱
        /// </summary>
        public string CategoryName { get; set; } = string.Empty;

        /// <summary>
        /// 實驗室代碼
        /// </summary>
        public string LabCode { get; set; } = string.Empty;

        /// <summary>
        /// 實驗室名稱
        /// </summary>
        public string LabName { get; set; } = string.Empty;

        /// <summary>
        /// 是否為主區域實驗室 (true: 主區域; false: 兼區域)
        /// </summary>
        public bool IsPrimary { get; set; }

        /// <summary>
        /// 職稱
        /// </summary>
        public string? JobTitle { get; set; }

        /// <summary>
        /// 實驗室權限生效起始時間
        /// </summary>
        public DateTimeOffset? EffectiveDate { get; set; }

        /// <summary>
        /// 實驗室權限失效截止時間
        /// </summary>
        public DateTimeOffset? ExpiryDate { get; set; }

        /// <summary>
        /// 該實驗室專屬指派之直接權限
        /// </summary>
        public List<PermissionAuditInfoItemDto> LabDirectPermissions { get; set; } = new();

        /// <summary>
        /// 該實驗室繼承之角色權限
        /// </summary>
        public List<PermissionAuditInfoItemDto> LabRolePermissions { get; set; } = new();

        /// <summary>
        /// 該實驗室最終生效之有效權限
        /// </summary>
        public List<PermissionAuditInfoItemDto> LabEffectivePermissions { get; set; } = new();
    }
}
