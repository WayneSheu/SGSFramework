using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SGSFramework.Persistent.Converters
{

    /// <summary>
    /// 這個 ValueConverter 用於將 DateTime 轉換為 UTC 時區的 DateTime，並在讀取時指定為 UTC。
    /// </summary>
    public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcDateTimeConverter() : base(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
        {
        }
    }
}
