using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;

namespace SGSFramework.Core.Abstractions.DbContexts
{
    /// <summary>
    /// 審計日誌專用的 DbContext 介面
    /// </summary>
    public interface IAuditDbContext
    {
        DbSet<AuditLogEntity> AuditLogs { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }

}
