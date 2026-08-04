using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace SGSFramework.Persistent.Repositories.Vector
{

    /// <summary>
    /// 經由方法級別泛型化，完美支援多資料庫上下交叉審查之全域單例 (Singleton) 智慧路由服務
    /// 穿透 EF Core MetaModel 元數據快取，動態判定欄位唯一性之智慧分流路由服務
    /// </summary>
    /// <summary>
    /// 透過 ServiceProvider 動態工廠解析，完美支援多資料庫隔離審查之全域單例 (Singleton) 智慧路由服務
    /// </summary>
    public class QueryRoutingService : IQueryRoutingService
    {
        private readonly IServiceProvider _serviceProvider;
        private static readonly ConcurrentDictionary<string, bool> _uniquenessCache = new();

        public QueryRoutingService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public bool IsUniqueColumn<TDbContext, TEntity>(string propertyName)
            where TDbContext : DbContext
            where TEntity : class
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException("欄位名稱不可為空。", nameof(propertyName));
            }

            string cacheKey = $"{typeof(TDbContext).FullName}.{typeof(TEntity).FullName}.{propertyName}";

            return _uniquenessCache.GetOrAdd(cacheKey, _ =>
            {
                try
                {
                    // ============================================================================
                    // 核心修正：動態向系統容器請求特定型別的 IDbContextFactory<TDbContext>
                    // ============================================================================
                    var factory = _serviceProvider.GetService<IDbContextFactory<TDbContext>>();
                    if (factory is null)
                    {
                        // 若應用程式層未註冊該 DbContext 的工廠，防禦性降級改為直接從 Scope 解析上下文實例
                        using var scope = _serviceProvider.CreateScope();
                        var dbContext = scope.ServiceProvider.GetService<TDbContext>();
                        return dbContext is null ? false : CheckUniqueness(dbContext, typeof(TEntity), propertyName);
                    }

                    using var context = factory.CreateDbContext();
                    return CheckUniqueness(context, typeof(TEntity), propertyName);
                }
                catch (Exception)
                {
                    return false;
                }
            });
        }

        private static bool CheckUniqueness(DbContext dbContext, Type entityType, string propertyName)
        {
            IEntityType? entityMetadata = dbContext.Model.FindEntityType(entityType);
            if (entityMetadata is null)
            {
                return false;
            }

            // 1. 主鍵驗證
            IKey? primaryKey = entityMetadata.FindPrimaryKey();
            if (primaryKey is not null && primaryKey.Properties.Any(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // 2. 唯一索引驗證
            System.Collections.Generic.IEnumerable<IIndex> indexes = entityMetadata.GetIndexes();
            foreach (IIndex? index in indexes)
            {
                if (index.IsUnique && index.Properties.Any(p => p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
