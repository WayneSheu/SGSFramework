using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Identity.Abstractions
{
    public interface ISystemRolePermissionSeedService
    {
        Task SeedPermissionsFromExcelAsync(CancellationToken cancellationToken = default);
    }
}
