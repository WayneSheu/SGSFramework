using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Application.Services;


namespace SGS.Modules.ORG.Application.Features.Laboratories.Query
{

    /// <summary>
    /// 
    /// </summary>`
    public class GetLaboratoriesQuery: IRequest<List<LaboratoryDto>>
    {
        

    }


    /// <summary>
    /// 
    /// </summary>
    public class GetLaboratoriesHandler : IRequestHandler<GetLaboratoriesQuery, List<LaboratoryDto>>
    {
   
        private readonly IConfiguration _config;
        private readonly ILogger<GetLaboratoriesHandler> _logger;
        private IOrganizationService _organizationService;

        public GetLaboratoriesHandler(IConfiguration config, ILogger<GetLaboratoriesHandler> logger, IOrganizationService organizationService)
        {
            _config = config;
            _logger = logger;
            _organizationService = organizationService;
        }

        public async Task<List<LaboratoryDto>> Handle(GetLaboratoriesQuery request, CancellationToken cancellationToken)
        {
            try
            {
                var laboratories =  await _organizationService.GetOrganizationTreeAsync();
                var result = laboratories.Select(x => new LaboratoryDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    ParentId = x.ParentId,
                    NodePath = x.NodePath,
                    Level = x.Level
                }).ToList();

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while retrieving laboratories.");
               return new List<LaboratoryDto>();
            }
        }

    }
}
