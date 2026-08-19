using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SGS.Modules.ORG.Application.Abstractions;
using SGS.Modules.ORG.Application.Features.Laboratories.Dtos;
using SGS.Modules.ORG.Infrastructure.Entities.Org;
using SGS.Modules.ORG.Infrastructure.Repositories;
using SGSFramework.Core.Results;
using SGSFramework.Core.Errors;

namespace SGS.Modules.ORG.Application.Features.Laboratories.Query
{

    /// <summary>
    /// 取得所有實驗室清單的查詢
    /// </summary>
    public sealed record GetLaboratoriesQuery : IRequest<Result<List<LaboratoryDto>>>;


    public sealed class GetLaboratoriesQueryHandler : IRequestHandler<GetLaboratoriesQuery, Result<List<LaboratoryDto>>>
    {
        private readonly ILaboratoryRepository _repository;
        private readonly ILogger<GetLaboratoriesQueryHandler> _logger;

        public GetLaboratoriesQueryHandler(
            ILaboratoryRepository repository,
            ILogger<GetLaboratoriesQueryHandler> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<List<LaboratoryDto>>> Handle(GetLaboratoriesQuery request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request, nameof(request));

            try
            {
                var laboratories = await _repository.GetAllAsync(cancellationToken);

                var dtos = laboratories.Select(lab => new LaboratoryDto
                {
                    Id = lab.Id,
                    Name = lab.Name,
                    Code = lab.Code,
                    ParentId = lab.ParentId
                    //IsActive = lab.IsActive
                }).ToList();

                _logger.LogInformation("Retrieved {Count} laboratories successfully.", dtos.Count);
                return Result<List<LaboratoryDto>>.Success(dtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while fetching all laboratories.");
                return Result.Failure<List<LaboratoryDto>>(
                         Error.Failure("Laboratory.RetrieveFailed", $"Failed to retrieve laboratories: {ex.Message}"));
             
            }
        }
    }

}
