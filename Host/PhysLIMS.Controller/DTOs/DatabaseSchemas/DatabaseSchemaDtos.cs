namespace SGSFramework.ApiInfrastructure.DTOs.DatabaseSchemas;

/// <summary>
/// 資料庫結構總覽 (SSD Table Schema 文件根節點)
/// </summary>
public sealed record DatabaseSchemaDto(
    string DatabaseName,
    string ServerVersion,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<string> Schemas,
    IReadOnlyList<TableSchemaDto> Tables,
    IReadOnlyList<MigrationHistoryDto> Migrations);

/// <summary>
/// 資料庫更新履歷 (取自 EF Core Migration 檔與 __EFMigrationsHistory)
/// </summary>
/// <param name="MigrationId">Migration 識別 (yyyyMMddHHmmss_Name)</param>
/// <param name="Name">Migration 名稱</param>
/// <param name="CreatedAt">Migration 建立時間 (由識別碼時間戳記換算為本地時間)</param>
/// <param name="Context">所屬 DbContext</param>
/// <param name="Schema">DbContext 預設 Schema</param>
/// <param name="IsApplied">是否已套用至資料庫</param>
/// <param name="ProductVersion">套用時的 EF Core 版本</param>
/// <param name="HasMigrationFile">程式中是否仍有對應 Migration 檔</param>
/// <param name="Changes">變更內容摘要 (依操作類型彙整)</param>
public sealed record MigrationHistoryDto(
    string MigrationId,
    string Name,
    DateTimeOffset? CreatedAt,
    string Context,
    string? Schema,
    bool IsApplied,
    string? ProductVersion,
    bool HasMigrationFile,
    IReadOnlyList<string> Changes);

/// <summary>
/// 資料表定義 (含建立 / 異動日期、欄位、索引與外鍵)
/// </summary>
public sealed record TableSchemaDto(
    int ObjectId,
    string Schema,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime ModifiedAt,
    long RowCount,
    string? TemporalType,
    string? HistoryTable,
    string? LedgerType,
    IReadOnlyList<ColumnSchemaDto> Columns,
    IReadOnlyList<IndexSchemaDto> Indexes,
    IReadOnlyList<ForeignKeySchemaDto> ForeignKeys)
{
    /// <summary>
    /// schema.table 完整名稱
    /// </summary>
    public string FullName => $"{Schema}.{Name}";
}

/// <summary>
/// 欄位屬性定義
/// </summary>
public sealed record ColumnSchemaDto(
    int Ordinal,
    string Name,
    string DataType,
    string FullDataType,
    short MaxLength,
    byte Precision,
    byte Scale,
    bool IsNullable,
    bool IsPrimaryKey,
    bool IsForeignKey,
    bool IsUnique,
    bool IsIdentity,
    string? IdentityInfo,
    bool IsComputed,
    string? ComputedDefinition,
    string? DefaultValue,
    string? Collation,
    string? ForeignKeyReference,
    string? Description)
{
    /// <summary>
    /// 資安保護標註 (取自 EF 模型屬性的 [Encrypted] / [Masked] / [AuditIgnore] 與稽核組態)；無任何保護時為 null
    /// </summary>
    public ColumnProtectionDto? Protection { get; init; }
}

/// <summary>
/// 欄位資安保護標註 (ISO 27001 個資盤點用)
/// </summary>
/// <param name="IsEncrypted">落地加密：[Encrypted] 或 Fluent API IsEncrypted()，資料庫內為密文</param>
/// <param name="MaskFormat">日誌遮罩格式 ([Masked] / [SensitiveData]) 的顯示名稱</param>
/// <param name="AuditHandling">資料日誌處理：Ignored = 完全不記錄；Masked = 僅記錄「已變更」不記錄值</param>
public sealed record ColumnProtectionDto(bool IsEncrypted, string? MaskFormat, string? AuditHandling);

/// <summary>
/// 索引定義
/// </summary>
public sealed record IndexSchemaDto(
    string Name,
    string Type,
    bool IsPrimaryKey,
    bool IsUnique,
    bool IsUniqueConstraint,
    string Columns,
    string? IncludedColumns,
    string? Filter);

/// <summary>
/// 外鍵關聯定義
/// </summary>
public sealed record ForeignKeySchemaDto(
    string Name,
    string Columns,
    string ReferencedSchema,
    string ReferencedTable,
    string ReferencedColumns,
    string OnDelete,
    string OnUpdate,
    bool IsDisabled)
{
    public string ReferencedFullName => $"{ReferencedSchema}.{ReferencedTable}";
}

/// <summary>
/// ERD 圖面 (已完成版面配置，前端 SVG 與後端 PDF 共用同一座標)
/// </summary>
public sealed record ErdDiagramDto(
    double Width,
    double Height,
    IReadOnlyList<ErdNodeDto> Nodes,
    IReadOnlyList<ErdEdgeDto> Edges);

/// <summary>
/// ERD 資料表節點
/// </summary>
public sealed record ErdNodeDto(
    string Id,
    string Schema,
    string Name,
    string? Description,
    double X,
    double Y,
    double Width,
    double Height,
    IReadOnlyList<ErdColumnDto> Columns);

/// <summary>
/// ERD 節點內的欄位列
/// </summary>
public sealed record ErdColumnDto(
    string Name,
    string DataType,
    bool IsPrimaryKey,
    bool IsForeignKey,
    bool IsNullable);

/// <summary>
/// ERD 關聯線 (Points 為折線座標序列，起點為子表 FK、終點為父表 PK)
/// </summary>
public sealed record ErdEdgeDto(
    string Name,
    string FromId,
    string ToId,
    string Label,
    bool IsSelfReference,
    IReadOnlyList<ErdPointDto> Points);

public sealed record ErdPointDto(double X, double Y);
