using Microsoft.EntityFrameworkCore;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Abstractions.Adapters;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Infrastructure.Adapters
{
    public sealed class LaboratoryMetadataProvider : ILaboratoryMetadataProvider
    {
        private readonly ORGDbContext _dbContext;

        public LaboratoryMetadataProvider(ORGDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<LaboratoryMetadataDto?> GetLaboratoryMetadataAsync(int labId, CancellationToken cancellationToken = default)
        {
            var lab = await _dbContext.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == labId, cancellationToken);

            if (lab is null) return null;

            return new LaboratoryMetadataDto(
                LabId: lab.Id,
                TenantLabId: lab.TenantLabId.Value,
                Category: lab.Code,
                DepartmentCode: lab.Code,
                Code: lab.Code,
                IsActive: lab.IsActive
            );
        }

        public async Task<LaboratoryMetadataDto?> GetLaboratoryMetadataAsync(Guid tenantLabId, CancellationToken cancellationToken = default)
        {
            var lab = await _dbContext.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.TenantLabId == tenantLabId, cancellationToken);

            if (lab is null) return null;

            return new LaboratoryMetadataDto(
                LabId: lab.Id,
                TenantLabId: lab.TenantLabId.Value,
                Category: lab.Code,
                DepartmentCode: lab.Code,
                Code: lab.Code,
                IsActive: lab.IsActive
            );
        }
    }
}
