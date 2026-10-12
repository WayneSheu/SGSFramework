using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.Paginations
{
    /// <summary>
    /// 分頁模式列舉
    /// </summary>
    public enum PaginationType
    {
        /// <summary>
        /// 偏移分頁 (Offset Pagination)模式
        /// </summary>
        Offset,

        /// <summary>
        /// 索引鍵式分頁模式 (Keyset Pagination / Cursor Pagination)
        /// </summary>
        Keyset
    }
}
