#nullable enable
namespace SGSFramework.Core.Logging
{
    /// <summary>
    /// 多個日誌資料庫（熱庫 + 冷庫）合併分頁的純函式：各來源無法在單一 SQL 合併，
    /// 呼叫端對每個來源各取「前 頁碼×每頁筆數」筆並加總筆數，再由此依時間合併切頁。
    /// </summary>
    public static class LogPageMerger
    {
        /// <summary>每個來源最多取回的筆數上限（避免深頁耗盡記憶體）</summary>
        public const int MaxMergeDepth = 5000;

        /// <summary>每個來源需取回的筆數（頁碼 × 每頁筆數，且不超過上限）；超出可檢視範圍擲出 <see cref="ArgumentException"/></summary>
        public static int TakePerSource(int pageIndex, int pageSize)
        {
            pageIndex = Math.Max(pageIndex, 1);
            pageSize = Math.Clamp(pageSize, 1, 500);

            if ((pageIndex - 1) * pageSize >= MaxMergeDepth)
            {
                throw new ArgumentException($"多來源合併查詢最多檢視前 {MaxMergeDepth} 筆，請縮小時間區間或增加篩選條件。");
            }

            return Math.Min(pageIndex * pageSize, MaxMergeDepth);
        }

        /// <summary>依時間（再依 Id）排序合併後切出指定頁；Id 僅用於同一時間的穩定排序。</summary>
        public static List<(T Item, string? Source)> MergePage<T>(
            IEnumerable<(T Item, string? Source)> rows,
            Func<T, DateTimeOffset> time,
            Func<T, long> id,
            int pageIndex,
            int pageSize,
            bool ascending)
        {
            ArgumentNullException.ThrowIfNull(rows);
            pageIndex = Math.Max(pageIndex, 1);
            pageSize = Math.Clamp(pageSize, 1, 500);

            var ordered = ascending
                ? rows.OrderBy(r => time(r.Item)).ThenBy(r => id(r.Item))
                : rows.OrderByDescending(r => time(r.Item)).ThenByDescending(r => id(r.Item));

            return ordered.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToList();
        }
    }
}
