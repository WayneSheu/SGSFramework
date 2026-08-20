using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.TreeNodes;
using System;
using System.Collections.Generic;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Dtos
{
    /// <summary>
    /// 組織/實驗室樹狀資料傳輸物件 (DTO)
    /// </summary>
    public sealed record LaboratoryDto : ITreeNode<int, LaboratoryDto>
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }

        /// <summary>
        /// 僅 Level 2 區域實驗室實體持有的 TenantLabId
        /// </summary>
        public Guid? TenantLabId { get; set; }

        /// <summary>
        /// 運算後生效的多租戶 ID (若自身為 null 則向父節點遞迴繼承 Level 2 之 TenantLabId)
        /// </summary>
        public Guid? EffectiveTenantLabId { get; set; }

        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Location { get; set; }
        public string? Description { get; set; }
        public string NodePath { get; set; } = string.Empty;
        public int Level { get; set; }

        /// <summary>
        /// 子組織/子實驗室節點清單
        /// </summary>
        public List<LaboratoryDto> Children { get; set; } = new List<LaboratoryDto>();
    }
}