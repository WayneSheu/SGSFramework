using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Dtos
{
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
        public bool IsDirectlyAssigned { get; init; }
    }
}
