using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Dtos
{
    public sealed record UserLabAssignmentDto(
        int LabId,
        Guid TenantLabId,
        bool IsPrimary,
        string? JobTitle,
        DateTime EffectiveDate,
        DateTime? ExpiryDate
    );
}
