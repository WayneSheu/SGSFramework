using MiniExcelLibs;
using MiniExcelLibs.OpenXml;
using SGSFramework.ApiInfrastructure.DTOs.DatabaseSchemas;

namespace SGSFramework.ApiInfrastructure.Services.DatabaseSchemas;

/// <summary>
/// SSD Table Schema Excel 匯出 (MiniExcel)：
/// 文件資訊、資料表清單、欄位明細、索引、外鍵關聯五個工作表，皆附篩選與凍結標題列。
/// </summary>
public static class SchemaExcelExporter
{
    private const string DateFormat = "yyyy-MM-dd HH:mm:ss";

    public static async Task<byte[]> ExportAsync(DatabaseSchemaDto schema, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var sheets = new Dictionary<string, object>
        {
            ["文件資訊"] = BuildInfo(schema),
            ["資料表清單"] = BuildTables(schema),
            ["欄位明細"] = BuildColumns(schema),
            ["索引"] = BuildIndexes(schema),
            ["外鍵關聯"] = BuildForeignKeys(schema),
            ["更新履歷"] = BuildMigrations(schema)
        };

        var configuration = new OpenXmlConfiguration
        {
            AutoFilter = true,
            FreezeRowCount = 1,
            FastMode = true,
            EnableAutoWidth = true,
            TableStyles = TableStyles.Default
        };

        using var stream = new MemoryStream();
        await stream.SaveAsAsync(sheets, configuration: configuration, cancellationToken: cancellationToken);
        return stream.ToArray();
    }

    private static List<Dictionary<string, object?>> BuildInfo(DatabaseSchemaDto schema) =>
    [
        Row(("項目", "資料庫名稱"), ("內容", schema.DatabaseName)),
        Row(("項目", "伺服器版本"), ("內容", schema.ServerVersion)),
        Row(("項目", "產出時間"), ("內容", schema.GeneratedAt.ToString(DateFormat))),
        Row(("項目", "Schema"), ("內容", string.Join(", ", schema.Tables.Select(t => t.Schema).Distinct()))),
        Row(("項目", "資料表數"), ("內容", schema.Tables.Count)),
        Row(("項目", "欄位數"), ("內容", schema.Tables.Sum(t => t.Columns.Count))),
        Row(("項目", "外鍵關聯數"), ("內容", schema.Tables.Sum(t => t.ForeignKeys.Count)))
    ];

    private static List<Dictionary<string, object?>> BuildTables(DatabaseSchemaDto schema) =>
        schema.Tables.Select((t, i) => Row(
            ("序號", i + 1),
            ("Schema", t.Schema),
            ("資料表名稱", t.Name),
            ("說明", t.Description),
            ("建立日期", t.CreatedAt.ToString(DateFormat)),
            ("最後異動日期", t.ModifiedAt.ToString(DateFormat)),
            ("資料筆數", t.RowCount),
            ("欄位數", t.Columns.Count),
            ("主鍵", string.Join(", ", t.Columns.Where(c => c.IsPrimaryKey).Select(c => c.Name))),
            ("系統版本控制", t.TemporalType),
            ("歷程資料表", t.HistoryTable),
            ("帳本類型", t.LedgerType))).ToList();

    private static List<Dictionary<string, object?>> BuildColumns(DatabaseSchemaDto schema) =>
        schema.Tables.SelectMany(t => t.Columns.Select(c => Row(
            ("Schema", t.Schema),
            ("資料表名稱", t.Name),
            ("序號", c.Ordinal),
            ("欄位名稱", c.Name),
            ("資料型別", c.FullDataType),
            ("長度", c.MaxLength == -1 ? "max" : c.MaxLength.ToString()),
            ("精確度", c.Precision),
            ("小數位數", c.Scale),
            ("允許 Null", Flag(c.IsNullable)),
            ("PK", Flag(c.IsPrimaryKey)),
            ("FK", Flag(c.IsForeignKey)),
            ("參考欄位", c.ForeignKeyReference),
            ("Unique", Flag(c.IsUnique)),
            ("Identity", c.IdentityInfo),
            ("計算欄位", c.ComputedDefinition),
            ("預設值", c.DefaultValue),
            ("定序", c.Collation),
            ("說明", c.Description)))).ToList();

    private static List<Dictionary<string, object?>> BuildIndexes(DatabaseSchemaDto schema) =>
        schema.Tables.SelectMany(t => t.Indexes.Select(i => Row(
            ("Schema", t.Schema),
            ("資料表名稱", t.Name),
            ("索引名稱", i.Name),
            ("類型", i.Type),
            ("PK", Flag(i.IsPrimaryKey)),
            ("Unique", Flag(i.IsUnique)),
            ("索引欄位", i.Columns),
            ("Include 欄位", i.IncludedColumns),
            ("篩選條件", i.Filter)))).ToList();

    private static List<Dictionary<string, object?>> BuildMigrations(DatabaseSchemaDto schema) =>
        schema.Migrations.Select((m, i) => Row(
            ("序號", i + 1),
            ("建立時間", m.CreatedAt?.ToString(DateFormat)),
            ("Migration 識別碼", m.MigrationId),
            ("名稱", m.Name),
            ("DbContext", m.Context),
            ("Schema", m.Schema),
            ("狀態", !m.HasMigrationFile ? "缺檔" : m.IsApplied ? "已套用" : "未套用"),
            ("EF Core 版本", m.ProductVersion),
            ("變更內容", string.Join("\n", m.Changes)))).ToList();

    private static List<Dictionary<string, object?>> BuildForeignKeys(DatabaseSchemaDto schema) =>
        schema.Tables.SelectMany(t => t.ForeignKeys.Select(fk => Row(
            ("Schema", t.Schema),
            ("資料表名稱", t.Name),
            ("FK 名稱", fk.Name),
            ("欄位", fk.Columns),
            ("參考資料表", fk.ReferencedFullName),
            ("參考欄位", fk.ReferencedColumns),
            ("ON DELETE", fk.OnDelete),
            ("ON UPDATE", fk.OnUpdate),
            ("已停用", Flag(fk.IsDisabled))))).ToList();

    private static string Flag(bool value) => value ? "Y" : string.Empty;

    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] cells) =>
        cells.ToDictionary(c => c.Key, c => c.Value);
}
