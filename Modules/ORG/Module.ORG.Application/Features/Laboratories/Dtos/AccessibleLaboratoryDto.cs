using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Dtos
{
    /// <summary>
    /// 使用者可存取實驗室 Context DTO (含派駐與繼承權限資訊)
    /// </summary>
    public sealed record AccessibleLaboratoryDto
    {
        public int Id { get; init; }
        public int? ParentId { get; init; }
        public Guid? TenantLabId { get; init; }
        public Guid? EffectiveTenantLabId { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string NodePath { get; init; } = string.Empty;
        public int Level { get; init; }

        /// <summary>
        /// 是否為直接派駐/綁定對應 (true: 直接派駐, false: 樹狀階層繼承)
        /// </summary>
        public bool IsDirectlyAssigned { get; init; }

        /// <summary>
        /// 是否為主要歸屬實驗室 (僅在 IsDirectlyAssigned = true 時有效)
        /// </summary>
        public bool IsPrimary { get; init; }

        /// <summary>
        /// 派駐職務名稱 (如: 負責人、檢驗員)
        /// </summary>
        public string? JobTitle { get; init; }

        /// <summary>
        /// 是否為區域層級實驗室節點
        /// </summary>
        public bool IsRegional { get; init; }


    }
}
