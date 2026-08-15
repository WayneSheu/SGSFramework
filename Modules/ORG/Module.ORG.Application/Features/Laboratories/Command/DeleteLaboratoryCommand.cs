using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGSFramework.Core.Errors;
using SGSFramework.Core.Results;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGS.Modules.ORG.Application.Abstractions;


namespace SGS.Modules.ORG.Application.Features.Laboratories.Command
{

    /// <summary>
    /// 刪除實驗室
    /// </summary>`
    public class DeleteLaboratoryCommand : IRequest<Result<bool>>
    {
        public int Id { get; set; }

    }




    /// <summary>
    ///
    /// </summary>
    public class DeleteLaboratoryHandler : IRequestHandler<DeleteLaboratoryCommand, Result<bool>>
    {

        private readonly IConfiguration _config;
        private readonly ILogger<AddLaboratoryHandler> _logger;
        private readonly IOrganizationService _organizationService;


        public DeleteLaboratoryHandler(IConfiguration config, ILogger<AddLaboratoryHandler> logger, IOrganizationService organizationService)
        {
            _config = config;
            _logger = logger;
            _organizationService=organizationService;

        }


        public async Task<Result<bool>> Handle(DeleteLaboratoryCommand command, CancellationToken cancellationToken)
        {
            try
            {
                // 1. 業務邏輯驗證
                var ordLab = await _organizationService.GetOrganizationByIdAsync(command.Id);
                if (ordLab == null)
                {
                    return Error.NotFound("Laboratory.NotFound", $"找不到ID為 {command.Id} 的實驗室。");
                }

                await _organizationService.DeleteOrganizationAsync(command.Id);
                var result = true;
                return Result.Success(result);
            }
            catch (Exception ex)
            {
                throw new Exception($"刪除實驗室失敗: {ex.Message}", ex);
            }
        }  
    }
}
