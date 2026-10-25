namespace SGSFramework.ApiInfrastructure.Services.DatabaseSchemas;

/// <summary>
/// 不屬於任何 EF 模型的系統資料表 (如 EF Core 遷移歷程表) 與系統欄位 (如 SQL Server 帳本欄位) 之內建說明；
/// 這類物件由 EF Core / SQL Server 自動產生，無法透過 [Comment] / HasComment 標註，故由文件工具統一提供。
/// </summary>
internal static class SystemTableDescriptions
{
    /// <summary>
    /// SQL Server 帳本資料表 (Ledger Table) 自動產生的隱藏系統欄位，任何資料表名稱皆適用
    /// </summary>
    private static readonly Dictionary<string, string> Columns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ledger_start_transaction_id"] = "帳本系統欄位：建立此資料列版本之交易 ID (SQL Server 自動維護)",
        ["ledger_end_transaction_id"] = "帳本系統欄位：刪除或更新此資料列版本之交易 ID；目前版本為 NULL (僅可更新帳本資料表)",
        ["ledger_start_sequence_number"] = "帳本系統欄位：建立此資料列版本之作業於交易內的序號 (SQL Server 自動維護)",
        ["ledger_end_sequence_number"] = "帳本系統欄位：刪除或更新此資料列版本之作業於交易內的序號；目前版本為 NULL (僅可更新帳本資料表)",
        ["ledger_transaction_id"] = "帳本檢視欄位：異動此資料列的交易 ID",
        ["ledger_sequence_number"] = "帳本檢視欄位：異動作業於交易內的序號",
        ["ledger_operation_type"] = "帳本檢視欄位：作業類型代碼 (1 = INSERT、2 = DELETE)",
        ["ledger_operation_type_desc"] = "帳本檢視欄位：作業類型說明 (INSERT / DELETE)"
    };

    private static readonly Dictionary<string, EntityDescriptions> Tables = new(StringComparer.OrdinalIgnoreCase)
    {
        ["__EFMigrationsHistory"] = new EntityDescriptions(
            "EF Core 資料庫遷移歷程：記錄已套用至此 Schema 的 Migration",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MigrationId"] = "Migration 識別碼 (建立時間 yyyyMMddHHmmss_名稱)",
                ["ProductVersion"] = "套用時的 EF Core 版本"
            })
    };

    /// <summary>
    /// 依資料表名稱 (不分 Schema) 取得內建說明；非系統資料表回傳 null
    /// </summary>
    public static EntityDescriptions? Find(string tableName) => Tables.GetValueOrDefault(tableName);

    /// <summary>
    /// 依欄位名稱取得系統欄位內建說明；非系統欄位回傳 null
    /// </summary>
    public static string? FindColumn(string columnName) => Columns.GetValueOrDefault(columnName);
}
