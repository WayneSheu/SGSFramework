namespace SGSFramework.ApiInfrastructure.DTOs.DatabaseSchemas;

/// <summary>
/// 資料庫結構查詢 / 匯出篩選條件 (皆為 Query String)
/// </summary>
public sealed class SchemaFilterRequest
{
    /// <summary>
    /// 限定 Schema (例：core、org)，未指定則為全部
    /// </summary>
    public string[]? Schemas { get; init; }

    /// <summary>
    /// 限定資料表完整名稱 (schema.table)，未指定則為篩選後 Schema 下的全部資料表
    /// </summary>
    public string[]? Tables { get; init; }

    /// <summary>
    /// 是否包含系統版本控制 (Temporal) 與帳本 (Ledger) 的歷程資料表
    /// </summary>
    public bool IncludeHistoryTables { get; init; }

    /// <summary>
    /// ERD 僅顯示 PK / FK 欄位 (資料表數量多時可縮小圖面)
    /// </summary>
    public bool KeysOnly { get; init; }

    /// <summary>
    /// PDF 是否附上 ERD 圖
    /// </summary>
    public bool IncludeErd { get; init; } = true;
}
