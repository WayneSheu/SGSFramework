using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace SGSFramework.Core.Paginations
{
    public class OffsetPaginationStrategy<T> : IPaginationStrategy<T>
    {
        public async Task<PagedResult<T>> ExecuteAsync(
            IQueryable<T> query,
            PagedQueryParameters<object> parameters,
            SortStrategyContext<T> sortContext,
            CancellationToken cancellationToken = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));

            // 1. 計算總筆數
            int totalCount = await query.CountAsync(cancellationToken);

            // 2. 套用動態排序
            var sortedQuery = sortContext.ApplySorting(query, parameters.Sorts);

            // 3. 執行 Skip / Take 偏移分頁
            var items = await sortedQuery
                .Skip((parameters.PageIndex - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToListAsync(cancellationToken);

            return new PagedResult<T>(items, totalCount, parameters.PageIndex, parameters.PageSize);
        }
    }
}
