namespace SGSFramework.Core.Identiies.Tenants
{
    /// <summary>
    /// 專案不須租戶隔離，則固定回傳 "dbo"
    /// </summary>
    public class NullTenantService : ITenantService
    {
        /// <summary>
        /// 如果專案不須租戶隔離，則固定回傳 "dbo"
        /// 這會影響 BaseDbContext 在 OnModelCreating 時產生的預設 Schema
        /// </summary>
        public string GetTenantId()
        {
            // 預設回傳 dbo，避免在建模或套用設定時造成空值問題
            return "dbo";
        }
        // 取得資料庫位置
        public string GetConnectionString()
        {
            return string.Empty;
        }

        public string GetSchemaName()
        {
            // 專案無租戶隔離時，明確回傳 dbo
            return "dbo";
        }


    }
}
