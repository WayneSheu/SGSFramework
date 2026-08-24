using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.DTOs.UserLabMappings
{
    /// <summary>
    /// 使用者可存取實驗室之 Context 傳輸物件 (包含多租戶與繼承權限資訊)
    /// </summary>
    public record AccessibleLaboratoryDto
    {
        /// <summary>
        /// 實驗室唯一識別碼 (外掛模組 Lab 識別碼)
        /// </summary>
        public Guid LabId { get; init; }

        /// <summary>
        /// 所屬 Level 2 區域實驗室 ID (Multi-Tenant 邊界)
        /// </summary>
        public Guid TenantLabId { get; init; }

        /// <summary>
        /// 實驗室代碼 (如: LAB-TP-01)
        /// </summary>
        public string Code { get; init; } = string.Empty;

        /// <summary>
        /// 實驗室名稱
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// 是否為主要歸屬實驗室
        /// </summary>
        public bool IsPrimary { get; init; }

        /// <summary>
        /// 職務名稱 (如: 負責人、兼任指導員)
        /// </summary>
        public string? JobTitle { get; init; }

        /// <summary>
        /// 是否為區域層級實驗室節點
        /// </summary>
        public bool IsRegional { get; init; }

        /// <summary>
        /// 是否透過樹狀結構繼承取得的存取權限 (false 表示為直接派駐對應)
        /// </summary>
        public bool IsInherited { get; init; }
    }
}
