namespace SGSFramework.SPAModulePlugin.Application.Queries;

using MediatR;
using SGSFramework.SPAModulePlugin.Application.Abstractions;
using SGSFramework.SPAModulePlugin.Domain.Enums;
using SGSFramework.SPAModulePlugin.Domain.ValueObjects;

public sealed record GetAuthorizedSPAModulesQuery(Guid UserId, SPAFrameworkType FrameworkType)
    : IRequest<IReadOnlyList<SPAModuleManifest>>;

public sealed class GetAuthorizedSPAModulesQueryHandler
    : IRequestHandler<GetAuthorizedSPAModulesQuery, IReadOnlyList<SPAModuleManifest>>
{
    private readonly ISPAModuleDiscoveryService _discoveryService;

    public GetAuthorizedSPAModulesQueryHandler(ISPAModuleDiscoveryService discoveryService)
    {
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
    }

    public async Task<IReadOnlyList<SPAModuleManifest>> Handle(GetAuthorizedSPAModulesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return await _discoveryService
                .GetAuthorizedModulesAsync(request.UserId, request.FrameworkType, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"無法取得使用者 [{request.UserId}] 的 SPA 動態模組清單", ex);
        }
    }
}