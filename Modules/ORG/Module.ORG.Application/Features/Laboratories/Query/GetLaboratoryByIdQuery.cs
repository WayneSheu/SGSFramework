using MediatR;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Application.Services;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Query
{

    public record GetLaboratoryByIdQuery(int Id) : IRequest<Result<LaboratoryDto>>;

    public class GetLaboratoryByIdQueryHandler : IRequestHandler<GetLaboratoryByIdQuery, Result<LaboratoryDto>>
    {
        private readonly IOrganizationService _orgService;

        public GetLaboratoryByIdQueryHandler(IOrganizationService orgService)
        {
            _orgService = orgService ?? throw new ArgumentNullException(nameof(orgService));
        }

        public async Task<Result<LaboratoryDto>> Handle(GetLaboratoryByIdQuery request, CancellationToken cancellationToken)
        {
            var entity = await _orgService.GetOrganizationByIdAsync(request.Id, cancellationToken);

            if (entity == null)
            {
                //失敗時可以使用 Error.NotFound() 方法來回傳一個 NotFound 的錯誤結果
                return Error.NotFound("ORG_LAB_NOT_FOUND", $"找不到 ID 為 {request.Id} 的實驗室。");
            }

            var dto = new LaboratoryDto
            {
                Id = entity.Id,
                ParentId = entity.ParentId,
                TenantLabId = entity.TenantLabId,
                Code = entity.Code,
                Name = entity.Name,
                Location = entity.Location,
                Description = entity.Description,
                NodePath = entity.NodePath,
                Level = entity.Level
            };

            //成功時也可以利用隱式轉換直接回傳 dto，或者寫 Result.Success(dto)
            return dto; // 或 Result.Success(dto)
        }
    }
}
