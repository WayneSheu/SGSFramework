using MediatR;
using Microsoft.EntityFrameworkCore;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Query
{
    public sealed record GetAccessibleLaboratoriesQuery(string UserId) : IRequest<Result<List<AccessibleLaboratoryDto>>>;

    public sealed class GetAccessibleLaboratoriesQueryHandler
        : IRequestHandler<GetAccessibleLaboratoriesQuery, Result<List<AccessibleLaboratoryDto>>>
    {
        private readonly ORGDbContext _dbContext;

        public GetAccessibleLaboratoriesQueryHandler(ORGDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<Result<List<AccessibleLaboratoryDto>>> Handle(
            GetAccessibleLaboratoriesQuery request,
            CancellationToken cancellationToken)
        {
            try
            {
                // 1. 取得使用者被授權的所有直屬實驗室 ID 與 NodePath (通常為 Level 2)
                var assignedLabs = await _dbContext.UserLaboratories
                    .AsNoTracking()
                    .Where(ul => ul.UserId == request.UserId)
                    .Select(ul => new
                    {
                        ul.OrganizationId,
                        ul.Organization.NodePath
                    })
                    .ToListAsync(cancellationToken);

                if (!assignedLabs.Any())
                {
                    return Result<List<AccessibleLaboratoryDto>>.Success(new List<AccessibleLaboratoryDto>());
                }

                var assignedOrgIds = assignedLabs.Select(x => x.OrganizationId).ToHashSet();

                // 2. 利用 NodePath LIKE 進行物化路徑階層匹配，撈出包含直接授權與其下方所有子節點 (Level 3+)
                var queryBuilder = _dbContext.Organizations.AsNoTracking();

                // 動態構建 OR 條件: NodePath LIKE '/1/2/%' OR NodePath LIKE '/1/5/%'
                IQueryable<Infrastructure.Entities.Org.Organization> accessibleQuery = null!;
                foreach (var lab in assignedLabs)
                {
                    var prefix = lab.NodePath;
                    if (accessibleQuery == null)
                    {
                        accessibleQuery = queryBuilder.Where(o => o.NodePath.StartsWith(prefix));
                    }
                    else
                    {
                        accessibleQuery = accessibleQuery.Union(queryBuilder.Where(o => o.NodePath.StartsWith(prefix)));
                    }
                }

                var allAccessibleNodes = await accessibleQuery
                    .Include(o => o.Parent)
                    .ToListAsync(cancellationToken);

                // 3. 轉換為 AccessibleLaboratoryDto 並計算 EffectiveTenantLabId
                var dtoList = allAccessibleNodes.Select(node => new AccessibleLaboratoryDto
                {
                    Id = node.Id,
                    ParentId = node.ParentId,
                    TenantLabId = node.TenantLabId,
                    EffectiveTenantLabId = node.EffectiveTenantLabId,
                    Code = node.Code,
                    Name = node.Name,
                    NodePath = node.NodePath,
                    Level = node.Level,
                    IsDirectlyAssigned = assignedOrgIds.Contains(node.Id)
                })
                .OrderBy(x => x.NodePath)
                .ToList();

                return Result<List<AccessibleLaboratoryDto>>.Success(dtoList);
            }
            catch (Exception ex)
            {
                return Error.Failure("ORG_ACCESSIBLE_LABS_FAILED", $"查詢可存取實驗室時發生錯誤: {ex.Message}");
            }
        }
    }

}
