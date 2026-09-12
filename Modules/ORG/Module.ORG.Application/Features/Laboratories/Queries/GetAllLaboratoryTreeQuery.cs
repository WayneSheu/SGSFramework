using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Infrastructure.Dbcontexts;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Queries
{
    /// <summary>
    /// 取得所有實驗室完整樹狀結構 Query
    /// </summary>
    public sealed record GetAllLaboratoryTreeQuery : IRequest<Result<List<LaboratoryTreeDto>>>;

    /// <summary>
    /// 取得所有實驗室完整樹狀結構 Query 處理器
    /// </summary>
    public sealed class GetAllLaboratoryTreeQueryHandler : IRequestHandler<GetAllLaboratoryTreeQuery, Result<List<LaboratoryTreeDto>>>
    {
        private readonly ORGDbContext _dbContext;
        private readonly ILogger<GetAllLaboratoryTreeQueryHandler> _logger;

        public GetAllLaboratoryTreeQueryHandler(
            ORGDbContext dbContext,
            ILogger<GetAllLaboratoryTreeQueryHandler> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<List<LaboratoryTreeDto>>> Handle(GetAllLaboratoryTreeQuery request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                // 1. 一次性由資料庫撈出所有未軟刪除之實驗室/組織節點
                var entities = await _dbContext.Organizations
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted)
                    .OrderBy(x => x.Level)
                    .ThenBy(x => x.Id)
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                // 2. 轉換為 DTO 列表
                var dtoList = entities.Select(e => new LaboratoryTreeDto
                {
                    Id = e.Id,
                    TenantLabId = e.TenantLabId,
                    ParentId = e.ParentId,
                    Code = e.Code,
                    Name = e.Name,
                    Location = e.Location,
                    Description = e.Description,
                    NodePath = e.NodePath,
                    Level = e.Level,
                    IsActive = e.IsActive,
                    Children = new List<LaboratoryTreeDto>()
                }).ToList();

                // 3. 在記憶體中建立字典進行高效 O(N) 樹狀結構組裝
                var dtoDict = dtoList.ToDictionary(x => x.Id);
                var rootNodes = new List<LaboratoryTreeDto>();

                foreach (var dto in dtoList)
                {
                    if (dto.ParentId.HasValue && dtoDict.TryGetValue(dto.ParentId.Value, out var parentDto))
                    {
                        parentDto.Children.Add(dto);
                    }
                    else
                    {
                        // 無上層 Id 或上層節點不存在者視為根節點
                        rootNodes.Add(dto);
                    }
                }

                return Result.Success(rootNodes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "取得所有實驗室樹狀結構時發生未預期錯誤");
                return Result.Failure<List<LaboratoryTreeDto>>(
                    Error.Failure("ORG_GET_ALL_LAB_TREE_ERROR", "取得所有實驗室樹狀結構失敗。"));
            }
        }
    }

}
