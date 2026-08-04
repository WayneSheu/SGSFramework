using Microsoft.EntityFrameworkCore;

namespace SGSFramework.Core.Abstractions.DbContexts
{
    /// <summary>
    /// 含有碳盤查活動數據的 DbContext 接口，專為 SQL Server 2025 向量索引優化設計
    /// </summary>
    public interface ICemsDbContext<TEntity> where TEntity : class
    {
        // 讓 DbSet 變成泛型，適配各個模組的實體
        DbSet<TEntity> ActivityDatas { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
