using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging;
using SGSFramework.ApiInfrastructure.DTOs.DatabaseSchemas;

namespace SGSFramework.ApiInfrastructure.Services.DatabaseSchemas;

/// <summary>
/// 資料庫更新履歷來源
/// </summary>
public interface IMigrationHistoryProvider
{
    /// <summary>
    /// 取得所有已註冊 DbContext 的 Migration 履歷 (新→舊)
    /// </summary>
    Task<IReadOnlyList<MigrationHistoryDto>> GetHistoryAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 自 EF Core Migration 檔 (IMigrationsAssembly) 讀取每支 Migration 的 Up 操作並彙整為變更摘要，
/// 再對照 __EFMigrationsHistory 標示是否已套用。
/// </summary>
public sealed class EfMigrationHistoryProvider : IMigrationHistoryProvider
{
    private const int MaxNamesPerChange = 8;

    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EfMigrationHistoryProvider> _logger;

    public EfMigrationHistoryProvider(IServiceProvider serviceProvider, TimeProvider timeProvider, ILogger<EfMigrationHistoryProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _serviceProvider = serviceProvider;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MigrationHistoryDto>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<MigrationHistoryDto>();

        foreach (var context in DbContextDiscovery.ResolveRegistered(_serviceProvider, _logger))
        {
            try
            {
                result.AddRange(await ReadContextAsync(context, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "讀取 DbContext {ContextType} 的 Migration 履歷失敗。", context.GetType().Name);
            }
        }

        return result
            .DistinctBy(m => (m.Context, m.MigrationId))
            .OrderByDescending(m => m.MigrationId, StringComparer.Ordinal)
            .ThenBy(m => m.Context, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<List<MigrationHistoryDto>> ReadContextAsync(DbContext context, CancellationToken cancellationToken)
    {
        var contextName = context.GetType().Name;
        var schema = context.Model.GetDefaultSchema();
        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var provider = context.Database.ProviderName ?? string.Empty;

        // 已套用紀錄 (含 EF 版本)；歷程資料表不存在時視為皆未套用
        var applied = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var historyRepository = context.GetService<IHistoryRepository>();
        if (await historyRepository.ExistsAsync(cancellationToken))
        {
            foreach (var row in await historyRepository.GetAppliedMigrationsAsync(cancellationToken))
            {
                applied[row.MigrationId] = row.ProductVersion;
            }
        }

        var list = new List<MigrationHistoryDto>();
        foreach (var (id, typeInfo) in migrationsAssembly.Migrations)
        {
            IReadOnlyList<string> changes;
            try
            {
                var migration = migrationsAssembly.CreateMigration(typeInfo, provider);
                changes = Summarize(migration.UpOperations);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "無法解析 Migration {MigrationId} 的操作內容。", id);
                changes = ["(無法解析變更內容)"];
            }

            list.Add(new MigrationHistoryDto(
                id,
                NameOf(id),
                ParseTimestamp(id),
                contextName,
                schema,
                applied.ContainsKey(id),
                applied.GetValueOrDefault(id),
                true,
                changes));
        }

        // 已套用但程式中已無檔案的 Migration
        foreach (var (id, version) in applied.Where(kv => !migrationsAssembly.Migrations.ContainsKey(kv.Key)))
        {
            list.Add(new MigrationHistoryDto(id, NameOf(id), ParseTimestamp(id), contextName, schema, true, version, false, ["(程式中已無對應 Migration 檔)"]));
        }

        return list;
    }

    /// <summary>
    /// 依操作類型彙整，例如「新增資料表 (3)：A、B、C」、「更新欄位說明 (270)」
    /// </summary>
    internal static IReadOnlyList<string> Summarize(IReadOnlyList<MigrationOperation> operations)
    {
        // 依操作類型分組 (保留首次出現順序)，每組記錄筆數與名稱
        var groups = new List<(string Kind, int Count, List<string> Names)>();
        foreach (var operation in operations)
        {
            var (kind, name) = Classify(operation);
            var index = groups.FindIndex(g => g.Kind == kind);
            if (index < 0)
            {
                groups.Add((kind, 0, []));
                index = groups.Count - 1;
            }

            var group = groups[index];
            if (!string.IsNullOrWhiteSpace(name) && !group.Names.Contains(name))
            {
                group.Names.Add(name);
            }

            groups[index] = group with { Count = group.Count + 1 };
        }

        if (groups.Count == 0)
        {
            return ["(無結構變更)"];
        }

        return groups.Select(g =>
        {
            if (g.Names.Count == 0)
            {
                return $"{g.Kind} ({g.Count})";
            }

            var shown = string.Join("、", g.Names.Take(MaxNamesPerChange));
            var more = g.Names.Count > MaxNamesPerChange ? $" 等 {g.Names.Count} 項" : string.Empty;
            return $"{g.Kind} ({g.Count})：{shown}{more}";
        }).ToList();
    }

    /// <summary>
    /// 操作分類與顯示名稱；僅變更說明 (Comment) 的 Alter 操作另歸一類，避免誤認為結構修改
    /// </summary>
    private static (string Kind, string? Name) Classify(MigrationOperation operation) => operation switch
    {
        EnsureSchemaOperation op => ("建立 Schema", op.Name),
        CreateTableOperation op => ("新增資料表", Table(op.Schema, op.Name)),
        DropTableOperation op => ("刪除資料表", Table(op.Schema, op.Name)),
        RenameTableOperation op => ("更名資料表", $"{op.Name} → {op.NewName}"),
        AlterTableOperation op when IsTableCommentOnly(op) => ("更新資料表說明", null),
        AlterTableOperation op => ("修改資料表", Table(op.Schema, op.Name)),
        AddColumnOperation op => ("新增欄位", $"{op.Table}.{op.Name}"),
        DropColumnOperation op => ("刪除欄位", $"{op.Table}.{op.Name}"),
        RenameColumnOperation op => ("更名欄位", $"{op.Table}.{op.Name} → {op.NewName}"),
        AlterColumnOperation op when IsColumnCommentOnly(op) => ("更新欄位說明", null),
        AlterColumnOperation op => ("修改欄位", $"{op.Table}.{op.Name}"),
        CreateIndexOperation op => ("新增索引", op.Name),
        DropIndexOperation op => ("刪除索引", op.Name),
        RenameIndexOperation op => ("更名索引", $"{op.Name} → {op.NewName}"),
        AddPrimaryKeyOperation op => ("新增主鍵", op.Name),
        DropPrimaryKeyOperation op => ("刪除主鍵", op.Name),
        AddForeignKeyOperation op => ("新增外鍵", op.Name),
        DropForeignKeyOperation op => ("刪除外鍵", op.Name),
        AddUniqueConstraintOperation op => ("新增唯一條件", op.Name),
        DropUniqueConstraintOperation op => ("刪除唯一條件", op.Name),
        AddCheckConstraintOperation op => ("新增檢查條件", op.Name),
        DropCheckConstraintOperation op => ("刪除檢查條件", op.Name),
        InsertDataOperation op => ("新增種子資料", op.Table),
        UpdateDataOperation op => ("更新種子資料", op.Table),
        DeleteDataOperation op => ("刪除種子資料", op.Table),
        SqlOperation => ("執行自訂 SQL", null),
        _ => (operation.GetType().Name.Replace("Operation", string.Empty, StringComparison.Ordinal), null)
    };

    /// <summary>
    /// 欄位僅說明 (Comment) 變更，型別 / Null / 預設值等皆相同
    /// </summary>
    private static bool IsColumnCommentOnly(AlterColumnOperation op)
    {
        var old = op.OldColumn;
        return op.Comment != old.Comment
            && op.ClrType == old.ClrType
            && op.ColumnType == old.ColumnType
            && op.IsNullable == old.IsNullable
            && op.MaxLength == old.MaxLength
            && op.Precision == old.Precision
            && op.Scale == old.Scale
            && op.IsUnicode == old.IsUnicode
            && op.IsFixedLength == old.IsFixedLength
            && op.IsRowVersion == old.IsRowVersion
            && Equals(op.DefaultValue, old.DefaultValue)
            && op.DefaultValueSql == old.DefaultValueSql
            && op.ComputedColumnSql == old.ComputedColumnSql
            && op.IsStored == old.IsStored
            && op.Collation == old.Collation;
    }

    private static bool IsTableCommentOnly(AlterTableOperation op) =>
        op.Comment != op.OldTable.Comment &&
        op.GetAnnotations().Select(a => (a.Name, a.Value)).SequenceEqual(op.OldTable.GetAnnotations().Select(a => (a.Name, a.Value)));

    private static string Table(string? schema, string name) => schema is null ? name : $"{schema}.{name}";

    private static string NameOf(string migrationId)
    {
        var index = migrationId.IndexOf('_');
        return index >= 0 ? migrationId[(index + 1)..] : migrationId;
    }

    /// <summary>
    /// Migration 識別碼前 14 碼為 UTC 建立時間 (yyyyMMddHHmmss)
    /// </summary>
    private DateTimeOffset? ParseTimestamp(string migrationId)
    {
        if (migrationId.Length < 14 ||
            !DateTime.TryParseExact(migrationId[..14], "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utc))
        {
            return null;
        }

        return TimeZoneInfo.ConvertTime(new DateTimeOffset(utc, TimeSpan.Zero), _timeProvider.LocalTimeZone);
    }
}
