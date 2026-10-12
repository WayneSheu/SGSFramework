using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Paginations
{
    /// <summary>
    /// 支援動態排序與雙分頁模式的查詢參數
    /// </summary>
    public record PagedQueryParameters<TFilter> where TFilter : class
    {
        public PaginationType Type { get; init; } = PaginationType.Offset;
        public int PageIndex { get; init; } = 1;
        public int PageSize { get; init; } = 10;
        public string? SearchTerm { get; init; }
        public TFilter? Filter { get; init; }
        public IReadOnlyList<SortCriteria> Sorts { get; init; } = Array.Empty<SortCriteria>();

        // Keyset 分頁專用游標（記錄上一頁最後一筆資料的排序欄位值與主鍵）
        public object? LastSortValue { get; init; }
        public object? LastIdValue { get; init; }
    }
}
