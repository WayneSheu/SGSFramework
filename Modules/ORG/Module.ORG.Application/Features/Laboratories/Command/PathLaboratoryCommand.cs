using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGSFramework.Core.Helpers;
using SGS.Modules.ORG.Application.Services;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Command
{

    /// <summary>
    /// 編輯實驗室
    /// </summary>
    public class PathLaboratoryCommand : IRequest<Result<LaboratoryDto>>
    {
        public int Id { get; set; }
        public string Name { get; set; }    
    }




    /// <summary>
    /// 編輯實驗室處理程序
    /// </summary>
    public class PathLaboratoryHandler : IRequestHandler<PathLaboratoryCommand, Result<LaboratoryDto>>
    {

        private readonly IConfiguration _config;
        private readonly ILogger<PathLaboratoryHandler> _logger;
        private readonly IOrganizationService _organizationService;


        public PathLaboratoryHandler(IConfiguration config, ILogger<PathLaboratoryHandler> logger, IOrganizationService organizationService)
        {
            _config = config;
            _logger = logger;
            _organizationService=organizationService;

        }


        public async Task<Result<LaboratoryDto>> Handle(PathLaboratoryCommand command, CancellationToken cancellationToken)
        {
            try
            {
                // 1. 業務邏輯驗證
                var ordLab = await _organizationService.GetOrganizationByIdAsync(command.Id);
                if (ordLab == null)
                {
                    return Error.NotFound("Laboratory.NotFound", $"找不到ID為 {command.Id} 的實驗室。");
                }


                if (string.IsNullOrWhiteSpace(command.Name))
                {
                    return Error.Validation("Laboratory.InvalidName", "實驗室名稱為必填欄位。");
                }


                var pathorg = new Organization
               {
                   Id = command.Id,
                   Name = command.Name
               };

                
                var  org =await _organizationService.PatchOrganizationTreeAsync(pathorg);

                if (org != null)
                {
                    var dto= new LaboratoryDto
                    {
                        Id  = org.Id,
                        Name = org.Name,
                        ParentId = org.ParentId,
                        NodePath = org.NodePath,
                        Level = org.Level
                    };

                    return Result.Success(dto);
                }
                else
                {
                    throw new Exception($"Failed to path laboratory with ID {command.Id}.");
                }
            }
            catch (Exception ex)
            {
                // 非預期系統崩潰，不在此處包成 Result，直接丟出讓基礎設施層的全域異常 Middleware 處理
                throw new InvalidOperationException($"編輯實驗室 {command.Name} 時發生未預期系統錯誤。", ex);
            }
        }  
    }
}
