using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Paginations
{
    /// <summary>
    /// 企業級通用泛型分頁查詢參數基礎模型
    /// </summary>
    /// <typeparam name="TFilter">篩選條件型別</typeparam>
    public record PagedQueryParameters<TFilter> where TFilter : class
    {
        public int PageIndex { get; init; } = 1;
        public int PageSize { get; init; } = 10;
        public string? SearchTerm { get; init; }
        public TFilter? Filter { get; init; }
    }
}
