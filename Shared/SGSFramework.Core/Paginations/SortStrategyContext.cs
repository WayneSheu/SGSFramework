using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Paginations
{
    /// <summary>
    /// 排序策略處理器與工廠
    /// </summary>
    ///<typeparam name = "T" > 實體型別 </ typeparam >
    public class SortStrategyContext<T>
    {
        private readonly Dictionary<string, ISortStrategy<T>> _strategies = new(StringComparer.OrdinalIgnoreCase);

        public SortStrategyContext<T> Register(string propertyName, ISortStrategy<T> strategy)
        {
            _strategies[propertyName] = strategy ?? throw new ArgumentNullException(nameof(strategy));
            return this;
        }

        public IQueryable<T> ApplySorting(IQueryable<T> query, IEnumerable<SortCriteria> sorts)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (sorts == null) return query;

            bool isFirst = true;
            foreach (var sort in sorts)
            {
                if (!string.IsNullOrWhiteSpace(sort.SortBy) && _strategies.TryGetValue(sort.SortBy, out var strategy))
                {
                    query = strategy.ApplySort(query, sort.Direction, !isFirst);
                    isFirst = false;
                }
            }

            return query;
        }
    }
}
