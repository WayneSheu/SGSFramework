namespace SGSFramework.Core.Abstractions.Entities.Base
{
    /// <summary>
    /// 樂觀鎖並行控制：實作此介面之實體由 DbContext 自動將 <see cref="RowVersion"/> 設為 SQL Server rowversion。
    /// 並行更新衝突時 SaveChanges 會拋出 DbUpdateConcurrencyException。
    /// </summary>
    public interface IConcurrencyAware
    {
        byte[] RowVersion { get; set; }
    }
}
