namespace SGSFramework.Core.Abstractions.Entities.CarbonEmissions
{
    /// <summary>
    ///  碳盤查活動數據實體接口
    /// </summary>
    public interface IActivityData
    {
        int Id { get; set; }           // 為了 SQL 2025 向量索引，必須是 int
        int TenantId { get; set; }
        int SiteId { get; set; }
        int InventoryYear { get; set; }
        decimal Amount { get; set; }
        string Unit { get; set; }

        // 核心：語義搜尋所需欄位
        string? ProcessedSummary { get; set; }
        Microsoft.Data.SqlTypes.SqlVector<float> Embedding { get; set; }
    }
}
