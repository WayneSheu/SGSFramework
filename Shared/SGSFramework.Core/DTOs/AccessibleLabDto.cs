using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace SGSFramework.Core.DTOs
{
    /// <summary>
    /// 使用者可存取/巡檢之實驗室/組織節點 DTO
    /// 用於支援多 Root 組織樹結構與高階主管無感切換體驗
    /// </summary>
    public class AccessibleLabDto
    {
        public required int LabId { get; set; }
        public required Guid TenantLabId { get; set; }
        public required string LabCode { get; set; } = string.Empty;
        public required string LabName { get; set; } = string.Empty;
        public required string Path { get; set; } = string.Empty;
        public required int HierarchyLevel { get; set; }
        public required bool IsPrimary { get; set; }

        /// <summary>
        /// 母實驗室 Int ID（子階自動清空不序列化）
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ParentLabId { get; set; }

        /// <summary>
        /// 母實驗室 Guid ID（子階自動清空不序列化）
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? ParentTenantLabId { get; set; }

        /// <summary>
        /// 母實驗室代碼（子階自動清空不序列化）
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ParentLabCode { get; set; }

        /// <summary>
        /// 母實驗室名稱（子階自動清空不序列化）
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ParentLabName { get; set; }



    }


}
