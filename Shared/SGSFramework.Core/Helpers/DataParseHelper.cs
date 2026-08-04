namespace SGSFramework.Core.Helpers
{
    public static class DataParseHelper
    {
        public static DateTime ParseTaiwanDate(string twDateStr)
        {
            if (string.IsNullOrWhiteSpace(twDateStr)) return DateTime.UtcNow;
            var parts = twDateStr.Split('/');
            if (parts.Length == 2 && int.TryParse(parts[0], out int twYear) && int.TryParse(parts[1], out int month))
            {
                return new DateTime(twYear + 1911, month, 1, 0, 0, 0, DateTimeKind.Utc);
            }
            return DateTime.UtcNow;
        }

        // 轉換為民國年格式的日期字串，格式為 "YYY/MM"
        public static string FormatTaiwanDate(DateTime date)
        {
            if (date == DateTime.MinValue || date == DateTime.MaxValue) return string.Empty;
            var year = date.Year - 1911;
            return $"{year}/{date.Month:D2}";
        }

        // 轉換為民國年格式的日期字串，格式為 "YYY/MM/DD"
        public static string FormatTaiwanDateWithDay(DateTime date)
        {
            if (date == DateTime.MinValue || date == DateTime.MaxValue) return string.Empty;
            var year = date.Year - 1911;
            return $"{year}/{date.Month:D2}/{date.Day:D2}";
        }

        // 轉換為民國年格式的日期字串，格式為 "YYY/MM/DD HH:mm:ss"
        public static string FormatTaiwanDateTime(DateTime date)
        {
            if (date == DateTime.MinValue || date == DateTime.MaxValue) return string.Empty;
            var year = date.Year - 1911;
            return $"{year}/{date.Month:D2}/{date.Day:D2} {date.Hour:D2}:{date.Minute:D2}:{date.Second:D2}";
        }

        // 轉換為民國年格式的日期字串，格式為 "YYY/MM/DD HH:mm"
        public static string FormatTaiwanDateTimeWithoutSecond(DateTime date)
        {
            if (date == DateTime.MinValue || date == DateTime.MaxValue) return string.Empty;
            var year = date.Year - 1911;
            return $"{year}/{date.Month:D2}/{date.Day:D2} {date.Hour:D2}:{date.Minute:D2}";
        }



    }
}
