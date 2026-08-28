using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Infrastructure.Entities.Org
{
    public sealed record UserAccessibleLabReadModel
    {
        public required Guid UserId { get; init; }
        public required int LabId { get; init; }
        public required Guid TenantLabId { get; init; }
        public required string LabCode { get; init; }
        public required string LabName { get; init; }
        public required string Path { get; init; }
        public required int HierarchyLevel { get; init; }
        public required bool IsPrimary { get; init; }
        public required int ParentLabId { get; init; }

        public required Guid ParentTenantLabId { get; init; }
        public required string ParentLabCode { get; init; }
        public required string ParentLabName { get; init; }
    }
}
