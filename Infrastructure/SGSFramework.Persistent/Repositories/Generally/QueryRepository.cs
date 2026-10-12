// Path: SGSFramework.Persistent/Repositories/Generally/QueryRepository.cs
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Persistence;

namespace SGSFramework.Persistent.Repositories.Generally
{
    public class QueryRepository<TEntity> : IQueryRepository<TEntity> where TEntity : class
    {
        private readonly DbContext _dbContext;

        public QueryRepository(DbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public IQueryable<TEntity> GetQueryable()
        {
            try
            {
                return _dbContext.Set<TEntity>().AsNoTracking();
            }
            catch (SqlException ex)
            {
                throw new InvalidOperationException("於資料庫上下文建立唯讀查詢串流時發生異常。", ex);
            }
        }

        public async Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(id);
            return await _dbContext.Set<TEntity>().FindAsync(new[] { id }, cancellationToken);
        }
    }
}