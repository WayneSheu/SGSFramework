using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Abstractions.Transactions
{
    /// <summary>
    /// 工作單元介面，負責管理持久層的事務邊界與變更追蹤
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>
        /// 開啟異步資料庫事務
        /// </summary>
        /// <param name="cancellationToken">異步取消權牌</param>
        /// <returns>異步事務作用域物件</returns>
        Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 異步保存所有尚未持久化的變更
        /// </summary>
        /// <param name="cancellationToken">異步取消權牌</param>
        /// <returns>影響的資料列數</returns>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
