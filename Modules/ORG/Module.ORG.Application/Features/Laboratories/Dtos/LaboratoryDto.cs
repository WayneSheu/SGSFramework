using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.TreeNodes;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Dtos
{


    public sealed record LaboratoryDto:ITreeNode<int, LaboratoryDto>
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public Guid? TenantLabId { get; set; }
        /// <summary>
        /// 運算後生效的多租戶 ID (若自身為 null 則繼承上層 Root 節點之 TenantLabId)
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