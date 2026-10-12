using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Paginations
{
    /// <summary>
    /// 分頁策略介面
    /// </summary>
    public interface IPaginationStrategy<T>
    {
        Task<PagedResult<T>> ExecuteAsync(
            IQueryable<T> query,
            PagedQueryParameters<object> parameters,
            SortStrategyContext<T> sortContext,
            CancellationToken cancellationToken = default);
    }
}
