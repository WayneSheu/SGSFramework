using System;
using System.Linq;
using System.Linq.Expressions;

namespace SGSFramework.Core.Paginations
{
    /// <summary>
    /// 排序策略介面
    /// </summary>
    /// <typeparam name = "T" > 實體型別 </ typeparam >
    public interface ISortStrategy<T>
    {
        IQueryable<T> ApplySort(IQueryable<T> query, SortDirection direction, bool isSecondary);
    }
}
