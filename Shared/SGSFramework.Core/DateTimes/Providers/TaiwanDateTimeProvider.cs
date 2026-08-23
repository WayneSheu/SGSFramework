using System;
using System.Collections.Generic;
using System.Text;

namespace SGSFramework.Core.DateTimes.Providers
{
    /// <summary>
    /// 提供台灣時區的日期時間服務
    /// </summary>
    public class TaiwanDateTimeProvider : IDateTimeProvider
    {
        private static readonly TimeZoneInfo _taipeiZone = TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time");

        public DateTimeOffset Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _taipeiZone);
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
