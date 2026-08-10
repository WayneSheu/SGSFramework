using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Application.Services;
using SGS.Modules.ORG.Infrastructure.Entities.Org;


namespace SGS.Modules.ORG.Application.Features.Laboratories.Query
{

    /// <summary>
    /// 
    /// </summary>`
    public class GetLaboratoriesQuery: IRequest<List<LaboratoryDto>>
    {
        

    }

    public class GetLaboratoriesQueryHandler : IRequestHandler<GetLaboratoriesQuery, List<LaboratoryDto>>
    {
        private readonly IOrganizationService _orgService;

        public GetLaboratoriesQueryHandler(IOrganizationService orgService)
        {
            _orgService = orgService ?? throw new ArgumentNullException(nameof(orgService));
        }

        public async Task<List<LaboratoryDto>> Handle(GetLaboratoriesQuery request, CancellationToken cancellationToken)
        {
            // 🔑 1. 取得所有未刪除的組織資料 (不能只呼叫 GetRoots()，否則子節點不會被載入)
            List<Organization> allActiveOrgs = await _orgService.GetAllActiveOrganizationsAsync(cancellationToken);

            if (allActiveOrgs == null || !allActiveOrgs.Any())
            {
                return new List<LaboratoryDto>();
            }

            // 🔑 2. 將全量資料組裝為階層樹狀結構 (Tree Structure)
            List<LaboratoryDto> tree = BuildTree(allActiveOrgs, parentId: null);

            return tree;
        }

        /// <summary>
        /// 高效能記憶體內樹狀結構組裝演算法
        /// </summary>
        private static List<LaboratoryDto> BuildTree(List<Organization> nodes, int? parentId)
        {
            return nodes
                .Where(n => n.ParentId == parentId)
                .Select(n => new LaboratoryDto
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
                    // 🔑 遞迴搜尋以自身 Id 為 ParentId 的子節點
                    Children = BuildTree(nodes, n.Id)
                })
                .ToList();
        }
    }


}
