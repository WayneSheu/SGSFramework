using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace SGSFramework.Core.Paginations
{
/// <summary>
    /// 通用運算式樹排序策略實作
    /// </summary>
    ///<typeparam name = "T" > 實體型別 </ typeparam >
    public class ExpressionSortStrategy<T> : ISortStrategy<T>
    {
        private readonly Expression<Func<T, object>> _keySelector;

        public ExpressionSortStrategy(Expression<Func<T, object>> keySelector)
        {
            _keySelector = keySelector ?? throw new ArgumentNullException(nameof(keySelector));
        }

        public IQueryable<T> ApplySort(IQueryable<T> query, SortDirection direction, bool isSecondary)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));

            if (!isSecondary)
            {
                return direction == SortDirection.Descending
                    ? query.OrderByDescending(_keySelector)
                    : query.OrderBy(_keySelector);
            }
            else
            {
                var orderedQuery = query as IOrderedQueryable<T>;
                if (orderedQuery == null)
                {
                    return direction == SortDirection.Descending
                        ? query.OrderByDescending(_keySelector)
                        : query.OrderBy(_keySelector);
                }

                return direction == SortDirection.Descending
                    ? orderedQuery.ThenByDescending(_keySelector)
                    : orderedQuery.ThenBy(_keySelector);
            }
        }
    }
}
