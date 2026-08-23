using SGSFramework.Core.Identiies.Tenants;

namespace SGSFramework.Persistent.Tenants
{
    /// <summary>
    /// 專案不須租戶隔離，則固定回傳 "core"
    /// </summary>
    public class NullTenantService : ITenantService
    {
        /// <summary>
        /// 如果專案不須租戶隔離，則固定回傳 "core"
        /// 這會影響 BaseDbContext 在 OnModelCreating 時產生的預設 Schema
        /// </summary>
        public string GetTenantId()
        {
            // 預設回傳 core，避免在建模或套用設定時造成空值問題
            return "core";
        }
        // 取得資料庫位置
        public string GetConnectionString()
        {
            return string.Empty;
        }

        public string GetSchemaName()
        {
            // 專案無租戶隔離時，明確回傳 core
            return "core";
        }


    }
}
