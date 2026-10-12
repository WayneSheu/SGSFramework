using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Paginations
{
    /// <summary>
    /// 動態排序參數模型
    /// </summary>
    public record SortCriteria
    {
        public string SortBy { get; init; } = string.Empty;
        public SortDirection Direction { get; init; } = SortDirection.Ascending;
    }
}
