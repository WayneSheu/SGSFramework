using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Transactions
{
    /// <summary>
    /// 異步資料庫事務作用域介面，提供事務提交與回滾控制
    /// </summary>
    public interface ITransactionScope : IAsyncDisposable
    {
        /// <summary>
        /// 異步提交當前事務變更
        /// </summary>
        /// <param name="cancellationToken">異步取消權牌</param>
        Task CommitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 異步撤銷/回滾當前事務變更
        /// </summary>
        /// <param name="cancellationToken">異步取消權牌</param>
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }
}
