using MediatR;
using SGS.Modules.ORG.Application.Abstractions;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Query
{

    /// <summary>
    /// 依據 Id 取得單一實驗室基本資訊的查詢
    /// </summary>
    /// <param name="Id"></param>
    public record GetLaboratoryQuery(int Id) : IRequest<Result<LaboratoryDto>>;

    /// <summary>
    /// 依據 ID 取得單一實驗室/組織之查詢處理器
    /// </summary>
    public sealed class GetLaboratoryQueryHandler : IRequestHandler<GetLaboratoryQuery, Result<LaboratoryDto>>
    {
        private readonly IOrganizationService _orgService;

        public GetLaboratoryQueryHandler(IOrganizationService orgService)
        {
            _orgService = orgService ?? throw new ArgumentNullException(nameof(orgService));
        }
         
        public async Task<Result<LaboratoryDto>> Handle(GetLaboratoryQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var entity = await _orgService.GetOrganizationByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

                if (entity is null)
                {
                    //利用隱式轉換直接回傳 Error 物件 (符合 Result<T> 轉型規範)
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

                //利用隱式轉換糖直接回傳 DTO 實體
                return dto;
            }
            catch (Exception ex)
            {
                //利用隱式轉換糖直接回傳 Error 物件 (符合 Result<T> 轉型規範)
                return Error.Failure("ORG_LAB_GET_BY_ID_FAILED", $"取得實驗室資料時發生未知錯誤: {ex.Message}");
            }
        }
    }
}
