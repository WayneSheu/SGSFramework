using Microsoft.EntityFrameworkCore;
using SGSFramework.Persistent.Converters;

namespace SGSFramework.Persistent.Helpers
{
    /// <summary>
    /// /// 這個靜態類別提供了一個擴充方法，用於在 Entity Framework Core 的模型配置階段套用 SES 系統的全域慣例與轉換政策。
    /// </summary>
    public static class PersistentConventionHelper
    {
        /// <summary>
        /// 系統全域慣例與轉換政策
        /// </summary>
        public static void ApplySESDefaultConventions(this ModelConfigurationBuilder configurationBuilder)
        {
            // 1. 自動索引慣例
            configurationBuilder.Conventions.Add(_ => new AutoIndexConvention());

            // 2. 動態 Decimal 精度與取捨標籤慣例
            configurationBuilder.Conventions.Add(_ => new DecimalPrecisionConvention());




            // 3. 全局預設配置：若無特別標註，decimal 預設為 (18, 6)
            configurationBuilder.Properties<decimal>().HavePrecision(18, 6);

            // 4. 其他現有配置
            // 4.1 Enum 轉字串
            configurationBuilder.Properties<Enum>().HaveConversion<string>();
            // 4.2 DateTime 轉 UTC
            configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();

        }
    }
}
