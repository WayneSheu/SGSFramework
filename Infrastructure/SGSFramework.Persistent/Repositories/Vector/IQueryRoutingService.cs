using Microsoft.EntityFrameworkCore;

namespace SGSFramework.Persistent.Repositories.Vector
{
    /// <summary>
    /// 支援多資料庫內容 (DbContext) 動態切換之智慧型欄位元數據審查介面
    /// </summary>
    public interface IQueryRoutingService
    {
        /// <summary>
        /// 分析特定實體之特定欄位，於指定之資料庫元數據中是否具備唯一性約束
        /// </summary>
        bool IsUniqueColumn<TDbContext, TEntity>(string propertyName)
            where TDbContext : DbContext
            where TEntity : class;
    }

}
