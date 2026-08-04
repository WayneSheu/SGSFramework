namespace SGSFramework.Core.Identiies.Tenants
{
    /// <summary>
    /// 定義租戶解析器介面
    /// </summary>
    public interface ITenantService
    {
        string GetSchemaName();      // 取得目前租戶應使用的 Schema
        string GetTenantId();
        string GetConnectionString(); // 取得資料庫位置


    }
}
