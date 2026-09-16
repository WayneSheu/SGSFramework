using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.SystemLog.DTOs
{
    /// <summary>
    /// 統一分頁回應封裝
    /// </summary>
    public record PagedResult<T>(
        IReadOnlyList<T> Items,
        long TotalCount,
        int PageIndex,
        int PageSize
    )
    {
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    }
}
