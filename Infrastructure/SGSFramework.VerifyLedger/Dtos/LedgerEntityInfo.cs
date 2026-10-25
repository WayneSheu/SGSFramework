namespace SGSFramework.VerifyLedger.Dtos
{
    /// <summary>
    /// 可進行總帳驗證的帳本資料表 (DbContext + 實作 ILedgerEntity 的實體)
    /// </summary>
    /// <param name="ContextName">DbContext 型別名稱 (驗證 / 報告路由的 contextName)</param>
    /// <param name="EntityName">實體型別名稱 (驗證 / 報告路由的 entityName)</param>
    /// <param name="Schema">資料表 Schema</param>
    /// <param name="TableName">資料表名稱</param>
    public record LedgerEntityInfo(string ContextName, string EntityName, string? Schema, string? TableName);
}
