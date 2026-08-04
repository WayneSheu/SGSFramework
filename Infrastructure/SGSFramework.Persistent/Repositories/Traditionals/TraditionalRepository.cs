using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace SGSFramework.Persistent.Repositories.Traditionals
{
    /// <summary>
    /// 支援泛型 DbContext 注入之通用傳統關係型資料倉儲實作
    /// </summary>
    public class TraditionalRepository<TDbContext, TEntity> : ITraditionalRepository<TDbContext, TEntity>
        where TDbContext : DbContext
        where TEntity : class
    {
        private readonly TDbContext _dbContext;

        /// <summary>
        /// 初始化通用常規倉儲，由 DI 儲存器動態配對目標 DbContext 實例
        /// </summary>
        public TraditionalRepository(TDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<TEntity?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber))
            {
                throw new ArgumentException("發票號碼不可為空值或空白字串。", nameof(invoiceNumber));
            }

            try
            {
                // 動態從動態配置的 TDbContext 中擷取目標實體集，並透過 LINQ 轉譯為參數化 RPC 指令
                return await _dbContext.Set<TEntity>()
                    .FirstOrDefaultAsync(e => EF.Property<string>(e, "InvoiceNumber") == invoiceNumber, cancellationToken);
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("於指定之資料庫上下文執行傳統 B-Tree 索引對齊時發生基礎結構異常。", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("執行常規發票編碼檢索時發生未預期的系統內核錯誤。", ex);
            }
        }

        public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("無效的主鍵識別碼 (Empty Guid)。", nameof(id));
            }

            try
            {
                return await _dbContext.Set<TEntity>().FindAsync(new object[] { id }, cancellationToken);
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("執行主鍵叢集索引查詢失敗。", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("執行主鍵檢索時發生未預期的系統內核錯誤。", ex);
            }
        }

        public async Task<IEnumerable<TEntity>> GetPagedListAsync(int pageIndex, int pageSize, CancellationToken cancellationToken)
        {
            if (pageIndex < 1) pageIndex = 1;
            if (pageSize < 1) pageSize = 10;

            try
            {
                return await _dbContext.Set<TEntity>()
                    .AsNoTracking()
                    .Skip((pageIndex - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync(cancellationToken);
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("執行常規分頁數據擷取時發生阻斷。", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("執行常規分頁數據處理時發生未預期的系統內核錯誤。", ex);
            }
        }
    }
}
