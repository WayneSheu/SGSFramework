using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Results;
using System;
using System.Collections.Generic;
using System.Text;
using SGSFramework.Core.Errors; 

namespace SGS.Modules.ORG.Application.Features.Laboratories.Queries 
{
    /// <summary>
    /// 取得區域層級/頂層實驗室清單查詢指令
    /// </summary>
    public record GetRegionalLaboratoriesQuery : IRequest<Result<List<LaboratoryDto>>>;

    /// <summary>
    /// 取得區域層級實驗室清單查詢處理器
    /// </summary>
    public class GetRegionalLaboratoriesQueryHandler : IRequestHandler<GetRegionalLaboratoriesQuery, Result<List<LaboratoryDto>>>
    {
        private readonly ORGDbContext _dbContext;
        private readonly ILogger<GetRegionalLaboratoriesQueryHandler> _logger;

        public GetRegionalLaboratoriesQueryHandler(
            ORGDbContext dbContext,
            ILogger<GetRegionalLaboratoriesQueryHandler> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<List<LaboratoryDto>>> Handle(
            GetRegionalLaboratoriesQuery request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                // 查詢區域層級實驗室清單，Level=2 表示區域層級，TenantLabId 不為空表示是有效的實驗室
                var laboratories = await _dbContext.Organizations
                    .AsNoTracking()
                    .Where(x => x.Level == 2 && x.TenantLabId != Guid.Empty && x.IsDeleted == false)
                    .OrderBy(x => x.Code)
                    .ThenBy(x => x.Name)
                    .Select(x => new LaboratoryDto
                    {
                        Id = x.Id,
                        ParentId = x.ParentId,
                        Code = x.Code,
                        Name = x.Name,
                        TenantLabId = x.TenantLabId,
                        Level = x.Level,
                        NodePath = x.NodePath,
                        Location = x.Location,
                        Description = x.Description
                    })
                    .ToListAsync(cancellationToken);

                return Result.Success(laboratories);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while fetching regional laboratories.");
                return Result.Failure<List<LaboratoryDto>>(
                    Error.Failure("ORG_REGIONAL_LABS_FETCH_ERROR", "無法順利取得區域實驗室清單。"));
            }
        }
    }


}
