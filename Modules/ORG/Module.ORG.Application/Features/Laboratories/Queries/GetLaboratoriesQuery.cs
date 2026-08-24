using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.ORG.Application.Abstractions;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Results;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Queries;

public record GetLaboratoriesQuery : IRequest<Result<List<LaboratoryDto>>>;

public class GetLaboratoriesQueryHandler : IRequestHandler<GetLaboratoriesQuery, Result<List<LaboratoryDto>>>
{
    private readonly ORGDbContext _dbContext;

    public GetLaboratoriesQueryHandler(ORGDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<Result<List<LaboratoryDto>>> Handle(GetLaboratoriesQuery request, CancellationToken cancellationToken)
    {
        var laboratories = await _dbContext.Organizations
            .AsNoTracking()
            .Select(org => new LaboratoryDto
            {
                Id = org.Id,
                ParentId = org.ParentId,
                Code = org.Code,
                Name = org.Name,
                Location = org.Location,
                Description = org.Description,
                NodePath = org.NodePath,
                Level = org.Level, // 補齊 Level 映射
                TenantLabId = org.TenantLabId, // 補齊 TenantLabId 映射
                EffectiveTenantLabId = org.TenantLabId // 依業務邏輯賦值
            })
            .ToListAsync(cancellationToken);

        return Result.Success(laboratories);
    }
}