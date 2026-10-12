using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SGSFramework.Core.Paginations
{
    /// <summary>
    /// 高效能索引鍵式分頁策略實作 (Keyset / Cursor Pagination)
    /// </summary>
    /// <typeparam name="T">實體型別</typeparam>
    public class KeysetPaginationStrategy<T> : IPaginationStrategy<T>
    {
        private readonly Expression<Func<T, object>> _idSelector;

        public KeysetPaginationStrategy(Expression<Func<T, object>> idSelector)
        {
            _idSelector = idSelector ?? throw new ArgumentNullException(nameof(idSelector));
        }

        public async Task<PagedResult<T>> ExecuteAsync(
            IQueryable<T> query,
            PagedQueryParameters<object> parameters,
            SortStrategyContext<T> sortContext,
            CancellationToken cancellationToken = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));

            // Keyset 分頁通常不執行昂貴的 CountAsync（回傳 -1 代表大數據極速模式）
            int totalCount = -1;

            // 取得第一筆排序條件作為 Keyset 定錨依據
            var primarySort = parameters.Sorts.FirstOrDefault();

            // 1. 套用動態排序
            var sortedQuery = sortContext.ApplySorting(query, parameters.Sorts);

            // 2. 若有帶入游標值（LastSortValue），則動態透過 Expression Tree 附加 Keyset 過濾條件
            if (primarySort != null && parameters.LastSortValue != null)
            {
                sortedQuery = ApplyKeysetFilter(
                    sortedQuery,
                    primarySort.SortBy,
                    primarySort.Direction,
                    parameters.LastSortValue,
                    parameters.LastIdValue);
            }

            // 3. 多抓一筆 (PageSize + 1) 用以判斷是否有下一頁 (HasNextPage)
            var takeCount = parameters.PageSize + 1;
            var fetchedItems = await sortedQuery
                .Take(takeCount)
                .ToListAsync(cancellationToken);

            bool hasNext = fetchedItems.Count > parameters.PageSize;
            var items = hasNext ? fetchedItems.Take(parameters.PageSize).ToList() : fetchedItems;

            return new PagedResult<T>(items, totalCount, parameters.PageIndex, parameters.PageSize);
        }

        /// <summary>
        /// 動態建立 Keyset Where 條件運算式
        /// 語法結構：(SortField < LastVal) OR (SortField == LastVal AND IdField < LastId) [若為 Descending]
        /// </summary>
        private IQueryable<T> ApplyKeysetFilter(
            IQueryable<T> query,
            string sortProperty,
            SortDirection direction,
            object lastSortValue,
            object? lastIdValue)
        {
            var parameter = Expression.Parameter(typeof(T), "x");

            // 取得排序屬性運算式
            var sortPropertyAccess = Expression.PropertyOrField(parameter, sortProperty);
            var convertedSortValue = ConvertValue(lastSortValue, sortPropertyAccess.Type);
            var sortConstant = Expression.Constant(convertedSortValue, sortPropertyAccess.Type);

            // 取得 ID 屬性運算式
            var idPropertyAccess = GetMemberExpression(_idSelector.Body);

            BinaryExpression comparison;
            Expression? equalityAndIdComparison = null;

            if (direction == SortDirection.Descending)
            {
                // 遞減：SortField < LastSortValue
                comparison = Expression.LessThan(sortPropertyAccess, sortConstant);

                if (lastIdValue != null && idPropertyAccess != null)
                {
                    var convertedIdValue = ConvertValue(lastIdValue, idPropertyAccess.Type);
                    var idConstant = Expression.Constant(convertedIdValue, idPropertyAccess.Type);

                    var equality = Expression.Equal(sortPropertyAccess, sortConstant);
                    var idComparison = Expression.LessThan(idPropertyAccess, idConstant);
                    equalityAndIdComparison = Expression.AndAlso(equality, idComparison);
                }
            }
            else
            {
                // 遞增：SortField > LastSortValue
                comparison = Expression.GreaterThan(sortPropertyAccess, sortConstant);

                if (lastIdValue != null && idPropertyAccess != null)
                {
                    var convertedIdValue = ConvertValue(lastIdValue, idPropertyAccess.Type);
                    var idConstant = Expression.Constant(convertedIdValue, idPropertyAccess.Type);

                    var equality = Expression.Equal(sortPropertyAccess, sortConstant);
                    var idComparison = Expression.GreaterThan(idPropertyAccess, idConstant);
                    equalityAndIdComparison = Expression.AndAlso(equality, idComparison);
                }
            }

            Expression finalExpression = comparison;
            if (equalityAndIdComparison != null)
            {
                finalExpression = Expression.OrElse(comparison, equalityAndIdComparison);
            }

            var lambda = Expression.Lambda<Func<T, bool>>(finalExpression, parameter);
            return query.Where(lambda);
        }

        private MemberExpression? GetMemberExpression(Expression expression)
        {
            if (expression is MemberExpression memberExpr) return memberExpr;
            if (expression is UnaryExpression { Operand: MemberExpression innerMember }) return innerMember;
            return null;
        }

        private object? ConvertValue(object? value, Type targetType)
        {
            if (value == null) return null;
            var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (value.GetType() == underlyingType) return value;
            return Convert.ChangeType(value, underlyingType);
        }
    }
}