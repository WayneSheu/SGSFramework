#nullable enable
using System.Globalization;
using System.Text.RegularExpressions;

namespace SGSFramework.Core.Logging
{
    /// <summary>換庫期間的粒度：整年度，或半年度（上半年 1–6 月、下半年 7–12 月）。</summary>
    public enum LogRotationGranularity
    {
        Year = 0,
        HalfYear = 1
    }

    /// <summary>
    /// 換庫封存的期間：整年度（Half = 0，例如 2026）、上半年（Half = 1，2026H1，1–6 月）或下半年（Half = 2，2026H2，7–12 月）。
    /// 冷庫名稱後綴即為 <see cref="Suffix"/>：{熱庫}_2026、{熱庫}_2026H1、{熱庫}_2026H2。
    /// </summary>
    public readonly record struct LogRotationPeriod(int Year, int Half = 0) : IComparable<LogRotationPeriod>
    {
        private static readonly Regex Pattern = new(@"^(\d{4})(?:[Hh]([12]))?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public bool IsValid => Year is >= LogRotationNaming.MinYear and <= LogRotationNaming.MaxYear && Half is >= 0 and <= 2;

        public int StartMonth => Half == 2 ? 7 : 1;

        public int EndMonth => Half == 1 ? 6 : 12;

        /// <summary>名稱後綴：2026、2026H1、2026H2</summary>
        public string Suffix => Year.ToString(CultureInfo.InvariantCulture) + (Half == 0 ? string.Empty : "H" + Half.ToString(CultureInfo.InvariantCulture));

        /// <summary>來源識別用的小寫鍵：2026、2026h1、2026h2</summary>
        public string Key => Suffix.ToLowerInvariant();

        public string DisplayName => Half switch
        {
            1 => $"{Year} 上半年（1–6 月）",
            2 => $"{Year} 下半年（7–12 月）",
            _ => $"{Year} 年度"
        };

        public LogRotationGranularity Granularity => Half == 0 ? LogRotationGranularity.Year : LogRotationGranularity.HalfYear;

        /// <summary>解析 2026 / 2026H1 / 2026h2（不分大小寫）；其他格式回傳 false</summary>
        public static bool TryParse(string? text, out LogRotationPeriod period)
        {
            period = default;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var match = Pattern.Match(text.Trim());
            if (!match.Success) return false;

            var year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var half = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;

            var candidate = new LogRotationPeriod(year, half);
            if (!candidate.IsValid) return false;

            period = candidate;
            return true;
        }

        /// <summary>兩個期間的月份區間是否重疊（整年度與同年的上 / 下半年互相重疊）</summary>
        public bool Overlaps(LogRotationPeriod other) =>
            Year == other.Year && StartMonth <= other.EndMonth && other.StartMonth <= EndMonth;

        /// <summary>下一個同粒度的期間：年度 +1；上半年 → 同年下半年；下半年 → 次年上半年</summary>
        public LogRotationPeriod Next() => Half switch
        {
            1 => new LogRotationPeriod(Year, 2),
            2 => new LogRotationPeriod(Year + 1, 1),
            _ => new LogRotationPeriod(Year + 1)
        };

        /// <summary>依時間先後排序（以期間結束月份為準；同年同結束月份者以名稱穩定排序）</summary>
        public int CompareTo(LogRotationPeriod other)
        {
            var byYear = Year.CompareTo(other.Year);
            if (byYear != 0) return byYear;

            var byEnd = EndMonth.CompareTo(other.EndMonth);
            return byEnd != 0 ? byEnd : Half.CompareTo(other.Half);
        }

        public override string ToString() => Suffix;
    }

    /// <param name="Period">建議的封存期間（已跳過重疊的既有冷庫）</param>
    /// <param name="Initial">依日期規則算出的原始建議；與 Period 不同表示因已有冷庫而往後推</param>
    public sealed record LogRotationSuggestion(LogRotationPeriod Period, LogRotationPeriod Initial)
    {
        public bool WasSkipped => Period != Initial;
    }

    /// <summary>
    /// 日誌庫年度 / 半年度換庫的命名與路徑規則（純函式，便於單元測試）：
    /// 冷庫名稱 = {熱庫}_{期間}；備份檔 = {熱庫}_rotation_{期間}_{full|diff}_{時間}.bak。
    /// </summary>
    public static class LogRotationNaming
    {
        public const int MinYear = 2000;
        public const int MaxYear = 2999;

        private static readonly Regex DatabasePattern = new(@"^[A-Za-z0-9_\-\.]{1,128}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>冷庫名稱：{熱庫}_{期間}。熱庫名稱或期間不合法時擲出 <see cref="ArgumentException"/>。</summary>
        public static string ColdDatabaseName(string hotDatabase, LogRotationPeriod period)
        {
            ValidateDatabaseName(hotDatabase, nameof(hotDatabase));
            ValidatePeriod(period);

            var name = $"{hotDatabase}_{period.Suffix}";
            ValidateDatabaseName(name, nameof(hotDatabase));
            return name;
        }

        public static void ValidatePeriod(LogRotationPeriod period)
        {
            if (!period.IsValid)
            {
                throw new ArgumentOutOfRangeException(nameof(period), period.ToString(), $"期間不合法：年度須介於 {MinYear} 至 {MaxYear}，上 / 下半年為 H1 / H2。");
            }
        }

        public static void ValidateDatabaseName(string? name, string paramName)
        {
            if (string.IsNullOrWhiteSpace(name) || !DatabasePattern.IsMatch(name))
            {
                throw new ArgumentException($"資料庫名稱不合法：'{name}'（僅允許英數、底線、連字號與句點，最長 128 字）。", paramName);
            }
        }

        /// <summary>
        /// 依日期與粒度算出預設封存期間（排程於期初執行、封存剛結束的期間；年中手動執行則封存目前所在期間）：
        /// 年度：1~6 月 → 上一年度；7~12 月 → 當年度。
        /// 半年度：1~3 月 → 上一年下半年；4~9 月 → 當年上半年；10~12 月 → 當年下半年。
        /// </summary>
        public static LogRotationPeriod DefaultPeriod(DateTimeOffset now, LogRotationGranularity granularity) =>
            granularity == LogRotationGranularity.HalfYear
                ? now.Month switch
                {
                    <= 3 => new LogRotationPeriod(now.Year - 1, 2),
                    <= 9 => new LogRotationPeriod(now.Year, 1),
                    _ => new LogRotationPeriod(now.Year, 2)
                }
                : new LogRotationPeriod(now.Month <= 6 ? now.Year - 1 : now.Year);

        /// <summary>
        /// 建議封存期間：從 <see cref="DefaultPeriod"/> 開始，與既有冷庫（含待核准者）重疊就往後推到下一個沒用過的期間。
        /// </summary>
        public static LogRotationSuggestion Suggest(DateTimeOffset now, LogRotationGranularity granularity, IEnumerable<LogRotationPeriod> existing)
        {
            var used = existing.ToList();
            var initial = DefaultPeriod(now, granularity);
            var candidate = initial;

            // 上限避免異常資料造成無窮迴圈；超過範圍時停在最後一個有效期間（核准時仍會被重疊檢查擋下）
            for (var i = 0; i < 1000 && candidate.IsValid && used.Any(u => u.Overlaps(candidate)); i++)
            {
                candidate = candidate.Next();
            }

            return new LogRotationSuggestion(candidate.IsValid ? candidate : initial, initial);
        }

        /// <summary>解析冷庫名稱 {熱庫}_{期間}；不符（含大小寫以外的多餘字元）回傳 false</summary>
        public static bool TryParseColdPeriod(string hotDatabase, string database, out LogRotationPeriod period)
        {
            period = default;
            var prefix = hotDatabase + "_";
            if (!database.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

            return LogRotationPeriod.TryParse(database[prefix.Length..], out period);
        }

        /// <summary>備份檔完整路徑（SQL Server 主機上的 Windows 路徑，一律以反斜線結尾補齊目錄）</summary>
        public static string BackupFilePath(string directory, string hotDatabase, LogRotationPeriod period, bool differential, DateTimeOffset now)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(directory);
            ValidateDatabaseName(hotDatabase, nameof(hotDatabase));
            ValidatePeriod(period);

            var dir = directory.TrimEnd('\\', '/') + "\\";
            var kind = differential ? "diff" : "full";
            return $"{dir}{hotDatabase}_rotation_{period.Suffix}_{kind}_{now.UtcDateTime:yyyyMMddHHmmss}.bak";
        }

        /// <summary>
        /// 由熱庫實體檔路徑推導冷庫檔路徑（取代檔名中的熱庫名稱）。檔名未含熱庫名稱時回傳 null，
        /// 以避免還原時覆蓋熱庫自己的檔案。
        /// </summary>
        public static string? DeriveColdFilePath(string hotPhysicalPath, string hotDatabase, string coldDatabase)
        {
            var directoryEnd = hotPhysicalPath.LastIndexOfAny(['\\', '/']);
            var directory = directoryEnd >= 0 ? hotPhysicalPath[..(directoryEnd + 1)] : string.Empty;
            var fileName = directoryEnd >= 0 ? hotPhysicalPath[(directoryEnd + 1)..] : hotPhysicalPath;

            // 只替換檔名（不動目錄名稱）；檔名必須含熱庫名稱，替換後才不會與熱庫檔案相同
            if (fileName.IndexOf(hotDatabase, StringComparison.OrdinalIgnoreCase) < 0) return null;

            var coldFileName = Regex.Replace(fileName, Regex.Escape(hotDatabase), coldDatabase.Replace("$", "$$"), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return directory + coldFileName;
        }

        /// <summary>跳脫 LIKE 萬用字元（搭配 ESCAPE '\')</summary>
        public static string EscapeLike(string value) =>
            value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

        /// <summary>LIKE 樣式：{熱庫}_ 開頭、後接 4 位數年度（其餘由 <see cref="TryParseColdPeriod"/> 在用戶端精確比對 H1 / H2 後綴）</summary>
        public static string ColdLikePattern(string hotDatabase) => EscapeLike(hotDatabase) + "\\_[0-9][0-9][0-9][0-9]%";
    }
}
