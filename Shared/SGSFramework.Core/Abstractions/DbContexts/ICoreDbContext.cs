using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.Identities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.DbContexts
{
    public interface ICoreDbContext
    {
        DbSet<UserLabMapping> UserLabMappings { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
