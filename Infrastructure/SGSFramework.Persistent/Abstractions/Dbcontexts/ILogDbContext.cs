using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.Logings;

namespace SGSFramework.Persistent.Abstractions.Dbcontexts
{
    //定義具備 Log 支援的 DbContext 介面
    //無論是主程式的 AppDbContext 還是各模組專屬的 Context，只要繼承自 SES.Persistent 的基類並實作該介面即可。
    public interface ILogDbContext
    {
        DbSet<SystemLog> SystemLogs { get; }

        DbSet<SecurityLog> SecurityLogs { get; } 

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }

}
