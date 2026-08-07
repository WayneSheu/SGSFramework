using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.Abstractions
{
    public interface IAdminSeederService
    {
        Task SeedAdminAsync(CancellationToken cancellationToken = default);
    }
}
