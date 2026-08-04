using SGSFramework.Core.Abstractions.Entities.Controller;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.ApiInfrastructure.Services
{
    public interface IControllerMetaService
    {
        Task<IReadOnlyList<ControllerMetadata>> GetActiveMetadataAsync(CancellationToken cancellationToken = default);
    }
}
