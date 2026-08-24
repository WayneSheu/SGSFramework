using MediatR;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Queries
{
    /// <summary>
    /// 依據 UserId 取得該使用者所有直接指派之 UserLabMapping 關聯清單 Query
    /// </summary>
    public sealed record GetUserLabMappingsByUserIdQuery(Guid UserId) : IRequest<Result<List<UserLabMappingDto>>>;

    /// <summary>
    /// GetUserLabMappingsByUserIdQuery 處理常式
    /// 依循 Clean Architecture 規範，透過 DbContext / IQueryable 進行高效率 Read-Only 查詢與 DTO 投影
    /// </summary>
    public sealed class GetUserLabMappingsByUserIdQueryHandler
    : IRequestHandler<GetUserLabMappingsByUserIdQuery, Result<List<UserLabMappingDto>>>
    {
        private readonly ICoreDbContext _coreContext;
        private readonly ORGDbContext _orgContext;

        public GetUserLabMappingsByUserIdQueryHandler(
            ICoreDbContext coreContext,
            ORGDbContext orgContext)
        {
            _coreContext = coreContext ?? throw new ArgumentNullException(nameof(coreContext));
            _orgContext = orgContext ?? throw new ArgumentNullException(nameof(orgContext));
        }

        public async Task<Result<List<UserLabMappingDto>>> Handle(
            GetUserLabMappingsByUserIdQuery request,
            CancellationToken cancellationToken)
        {
            // 1. 從 ICoreDbContext 撈取 UserLabMapping[cite: 6]
            var mappings = await _coreContext.UserLabMappings
                .AsNoTracking()
                .Where(m => m.UserId == request.UserId)
                .ToListAsync(cancellationToken);

            if (!mappings.Any()) return Result.Success(new List<UserLabMappingDto>());

            // 2. 從 ORGDbContext 撈取 Organization 並於記憶體組裝[cite: 6, 8]
            var labIds = mappings.Select(m => m.LabId).Distinct().ToList();
            var labs = await _orgContext.Organizations
                .AsNoTracking()
                .Where(o => labIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, cancellationToken);

            var result = mappings.Select(m => {
                labs.TryGetValue(m.LabId, out var lab);
                return new UserLabMappingDto
                {
                    UserId = m.UserId,
                    LabId = m.LabId,
                    TenantLabId = m.TenantLabId,
                    LabName = lab?.Name ?? string.Empty,
                    LabCode = lab?.Code ?? string.Empty,
                    IsPrimary = m.IsPrimary,
                    JobTitle = m.JobTitle,
                    EffectiveDate = m.EffectiveDate,
                    ExpiryDate = m.ExpiryDate,
                    IsActive = m.IsActive,
                    CreatedAtUtc = m.CreatedAtUtc
                };
            }).ToList();

            return Result.Success(result);
        }
    }
}
