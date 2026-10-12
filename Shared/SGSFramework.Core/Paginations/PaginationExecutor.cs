using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace SGSFramework.Core.Paginations
{
    public static class PaginationExecutor
    {
        public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
            this IQueryable<T> query,
            PagedQueryParameters<object> parameters,
            SortStrategyContext<T> sortContext,
            Expression<Func<T, object>>? idSelector = null,
            CancellationToken cancellationToken = default)
        {
            IPaginationStrategy<T> strategy = parameters.Type switch
            {
                PaginationType.Keyset when idSelector != null => new KeysetPaginationStrategy<T>(idSelector),
                _ => new OffsetPaginationStrategy<T>()
            };

            return await strategy.ExecuteAsync(query, parameters, sortContext, cancellationToken);
        }
    }
}
