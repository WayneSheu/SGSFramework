using MediatR;
using SGS.Modules.ORG.Application.Abstractions;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Query
{
    public record GetLaboratoryTreeByIdQuery(int Id) : IRequest<Result<LaboratoryDto>>;

    public class GetLaboratoryTreeByIdQueryHandler : IRequestHandler<GetLaboratoryTreeByIdQuery, Result<LaboratoryDto>>
    {
        private readonly IOrganizationService _orgService;

        public GetLaboratoryTreeByIdQueryHandler(IOrganizationService orgService)
        {
            _orgService = orgService ?? throw new ArgumentNullException(nameof(orgService));
        }

        public async Task<Result<LaboratoryDto>> Handle(GetLaboratoryTreeByIdQuery request, CancellationToken cancellationToken)
        {
            // 1. 取得目標節點
            var targetEntity = await _orgService.GetOrganizationByIdAsync(request.Id, cancellationToken);
            if (targetEntity == null)
            {
                return Error.NotFound("ORG_LAB_NOT_FOUND", $"找不到 ID 為 {request.Id} 的實驗室。");
            }

            // 2. 依據 NodePath 取得該節點及其所有下階層節點
            List<Organization> subtreeOrgs = await _orgService.GetSubtreeByNodePathAsync(targetEntity.NodePath, cancellationToken);

            if (subtreeOrgs == null || !subtreeOrgs.Any())
            {
                return Error.NotFound("ORG_LAB_TREE_EMPTY", $"無法載入 ID 為 {request.Id} 的子樹資料。");
            }

            // 3. 進行記憶體內樹狀組裝 (以 targetEntity.Id 為 Root)
            LaboratoryDto? treeRoot = BuildSubtree(subtreeOrgs, targetEntity.Id);

            if (treeRoot == null)
            {
                return Error.NotFound("ORG_LAB_TREE_BUILD_FAILED", $"組裝 ID 為 {request.Id} 的階層結構失敗。");
            }

            return treeRoot;
        }

        /// <summary>
        /// 子樹記憶體內組裝演算法
        /// </summary>
        private static LaboratoryDto? BuildSubtree(List<Organization> nodes, int rootId)
        {
            var dtoMap = nodes.ToDictionary(
                n => n.Id,
                n => new LaboratoryDto
                {
                    Id = n.Id,
                    ParentId = n.ParentId,
                    TenantLabId = n.TenantLabId,
                    Code = n.Code,
                    Name = n.Name,
                    Location = n.Location,
                    Description = n.Description,
                    NodePath = n.NodePath,
                    Level = n.Level,
                    Children = new List<LaboratoryDto>()
                });

            LaboratoryDto? rootDto = null;

            foreach (var node in nodes)
            {
                if (!dtoMap.TryGetValue(node.Id, out var currentDto)) continue;

                if (node.Id == rootId)
                {
                    rootDto = currentDto;
                }

                // 若 Parent 存在於當前子樹集合中，自動掛載至父節點的 Children 下
                if (node.ParentId.HasValue && dtoMap.TryGetValue(node.ParentId.Value, out var parentDto))
                {
                    parentDto.Children.Add(currentDto);
                }
            }

            return rootDto;
        }
    }
}
