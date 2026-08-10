using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Application.Services;


namespace SGS.Modules.ORG.Application.Features.Laboratories.Command
{

    /// <summary>
    /// 新增實驗室
    /// </summary>`
    public record AddLaboratoryCommand : IRequest<Result<LaboratoryDto>>
    {
        public int? ParentId { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Location { get; init; }
        public string? Description { get; init; }

        /// <summary>
        /// 可選：若前端未傳入，Command Handler 會根據 Parent 繼承或自動產生 UUIDv7
        /// </summary>
        public Guid? ExplicitTenantLabId { get; init; }
    }




    /// <summary>
    ///
    /// </summary>
    public class AddLaboratoryHandler : IRequestHandler<AddLaboratoryCommand, Result<LaboratoryDto>>
    {

        private readonly IConfiguration _config;
        private readonly ILogger<AddLaboratoryHandler> _logger;
        private readonly IOrganizationService _organizationService;


        public AddLaboratoryHandler(IConfiguration config, ILogger<AddLaboratoryHandler> logger, IOrganizationService organizationService)
        {
            _config = config;
            _logger = logger;
            _organizationService=organizationService;

        }


        public async Task<Result<LaboratoryDto>> Handle(AddLaboratoryCommand command, CancellationToken cancellationToken)
        {
            try
            {

                // 1. 業務邏輯驗證
                if (string.IsNullOrWhiteSpace(command.Name))
                {
                    return Error.Validation("Laboratory.InvalidName", "實驗室名稱為必填欄位。"); 
                }


                var org = new Organization
                {
                    ParentId = command.ParentId,
                    Code = command.Code,
                    Name = command.Name,
                    Description = command.Description
                };

               var  neworg =await _organizationService.CreateOrganizationAsync(org);

                if (neworg != null)
                {
                    // 2. 成功建立實驗室，回傳 DTO
                    var dto= new LaboratoryDto
                    {
                        Id = neworg.Id,
                        Code = neworg.Code,
                        Name = neworg.Name,
                        Description = neworg.Description,
                        ParentId = neworg.ParentId,
                        NodePath = neworg.NodePath,
                        Level = neworg.Level
                    };

                    return Result.Success(dto);
                }
                else
                {
                    throw new InvalidOperationException($"建立實驗室 {command.Name} 失敗，但未拋出例外。");
                }
            }
            catch (Exception ex)
            {
                // 非預期系統崩潰，不在此處包成 Result，直接丟出讓基礎設施層的全域異常 Middleware 處理
                throw new InvalidOperationException($"建立實驗室 {command.Name} 時發生未預期系統錯誤。", ex);
            }
        }  
    }
}
