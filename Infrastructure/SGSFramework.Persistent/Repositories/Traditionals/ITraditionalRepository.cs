using Microsoft.EntityFrameworkCore;

namespace SGSFramework.Persistent.Repositories.Traditionals
{
    /// <summary>
    /// 支援多資料庫內容 (DbContext) 動態切換之常規結構化數據倉儲介面
    /// </summary>
    /// <typeparam name="TDbContext">繼承自 DbContext 的目標資料庫上下文型別</typeparam>
    /// <typeparam name="TEntity">繫結的實體模型型別</typeparam>
    public interface ITraditionalRepository<TDbContext, TEntity>
        where TDbContext : DbContext
        where TEntity : class
    {
        /// <summary>
        /// 透過精確的發票號碼獲取單筆實體數據 (對齊傳統 B-Tree 唯一索引)
        /// </summary>
        Task<TEntity?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken);

        /// <summary>
        /// 依據主鍵 GUID 進行標準高效對齊
        /// </summary>
        Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

        /// <summary>
        /// 常規條件分頁查詢 (非向量化之結構化數據檢索)
        /// </summary>
        Task<IEnumerable<TEntity>> GetPagedListAsync(int pageIndex, int pageSize, CancellationToken cancellationToken);
    }
}
