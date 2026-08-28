using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.DTOs
{
    /// <summary>
    /// 依 Parent 母實驗室分組之組織節點群組 DTO
    /// </summary>
    public class AccessibleLabGroupDto
    {
        public int? ParentLabId { get; set; }
        public Guid? ParentTenantLabId { get; set; }
        public string ParentLabCode { get; set; } = string.Empty;
        public string ParentLabName { get; set; } = string.Empty;
        public List<AccessibleLabDto> Labs { get; set; } = [];

        /// <summary>
        /// 將扁平化之 AccessibleLabDto 轉譯為階層分組，並自動將子階層 Parent 屬性剔除
        /// </summary>
        public static List<AccessibleLabGroupDto> CreateGroupedList(IEnumerable<AccessibleLabDto> flatLabs)
        {
            ArgumentNullException.ThrowIfNull(flatLabs);

            return flatLabs
                .GroupBy(x => new
                {
                    x.ParentLabId,
                    x.ParentTenantLabId,
                    ParentLabCode = x.ParentLabCode ?? string.Empty,
                    ParentLabName = x.ParentLabName ?? string.Empty
                })
                .Select(g => new AccessibleLabGroupDto
                {
                    ParentLabId = g.Key.ParentLabId,
                    ParentTenantLabId = g.Key.ParentTenantLabId,
                    ParentLabCode = g.Key.ParentLabCode,
                    ParentLabName = g.Key.ParentLabName,
                    Labs = g.Select(lab => new AccessibleLabDto
                    {
                        LabId = lab.LabId,
                        TenantLabId = lab.TenantLabId,
                        LabCode = lab.LabCode,
                        LabName = lab.LabName,
                        Path = lab.Path,
                        HierarchyLevel = lab.HierarchyLevel,
                        IsPrimary = lab.IsPrimary,
                        // 將子階層 Parent 屬性設為 null，觸發 JsonIgnoreCondition.WhenWritingNull 隱藏欄位
                        ParentLabId = null,
                        ParentTenantLabId = null,
                        ParentLabCode = null,
                        ParentLabName = null
                    }).ToList()
                })
                .ToList();
        }
    }
}
