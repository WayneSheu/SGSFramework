using SGSFramework.Core.Abstractions.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Dtos
{
    public class LaboratoryDto
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public Guid? TenantLabId { get; set; }
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