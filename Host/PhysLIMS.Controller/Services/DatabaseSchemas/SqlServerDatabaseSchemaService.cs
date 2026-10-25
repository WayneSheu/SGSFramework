using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SGSFramework.ApiInfrastructure.DTOs.DatabaseSchemas;

namespace SGSFramework.ApiInfrastructure.Services.DatabaseSchemas;

/// <summary>
/// 以 SQL Server 系統目錄 (sys.tables / sys.columns / sys.indexes / sys.foreign_keys / MS_Description)
/// 產生資料庫結構，僅讀取中繼資料，不觸及任何業務資料列。
/// </summary>
/// <remarks>
/// 可見範圍受連線帳號的 VIEW DEFINITION 權限限制：無權限的物件不會出現在結果中。
/// </remarks>
public sealed class SqlServerDatabaseSchemaService : IDatabaseSchemaService
{
    private const string TablesSql = """
        SELECT t.object_id, s.name, t.name, CAST(ep.value AS nvarchar(max)), t.create_date, t.modify_date,
               ISNULL((SELECT SUM(p.rows) FROM sys.partitions p WHERE p.object_id = t.object_id AND p.index_id IN (0, 1)), 0),
               t.temporal_type, t.temporal_type_desc, hs.name + N'.' + ht.name,
               {0}
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        LEFT JOIN sys.extended_properties ep ON ep.class = 1 AND ep.major_id = t.object_id AND ep.minor_id = 0 AND ep.name = N'MS_Description'
        LEFT JOIN sys.tables ht ON ht.object_id = t.history_table_id
        LEFT JOIN sys.schemas hs ON hs.schema_id = ht.schema_id
        WHERE t.is_ms_shipped = 0 AND t.name <> N'sysdiagrams'
        """;

    // ledger_type 自 SQL Server 2022 起提供，舊版以 NULL 取代
    private const string LedgerColumnsSql = "t.ledger_type, t.ledger_type_desc, t.is_dropped_ledger_table";
    private const string NoLedgerColumnsSql = "CAST(NULL AS tinyint), CAST(NULL AS nvarchar(60)), CAST(0 AS bit)";

    private const string ColumnsSql = """
        SELECT c.object_id, c.column_id, c.name, ty.name, c.max_length, c.precision, c.scale, c.is_nullable,
               c.is_identity, CAST(ic.seed_value AS nvarchar(50)), CAST(ic.increment_value AS nvarchar(50)),
               c.is_computed, cc.definition, dc.definition, c.collation_name, CAST(ep.value AS nvarchar(max))
        FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        LEFT JOIN sys.identity_columns ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        LEFT JOIN sys.computed_columns cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id
        LEFT JOIN sys.default_constraints dc ON dc.object_id = c.default_object_id
        LEFT JOIN sys.extended_properties ep ON ep.class = 1 AND ep.major_id = c.object_id AND ep.minor_id = c.column_id AND ep.name = N'MS_Description'
        WHERE t.is_ms_shipped = 0
        ORDER BY c.object_id, c.column_id
        """;

    private const string IndexesSql = """
        SELECT i.object_id, i.index_id, i.name, i.type_desc, i.is_primary_key, i.is_unique, i.is_unique_constraint,
               i.filter_definition, c.name, ic.is_included_column, ic.is_descending_key
        FROM sys.indexes i
        JOIN sys.tables t ON t.object_id = i.object_id
        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE t.is_ms_shipped = 0 AND i.type > 0 AND i.is_hypothetical = 0
        ORDER BY i.object_id, i.index_id, ic.is_included_column, ic.key_ordinal, ic.index_column_id
        """;

    private const string ForeignKeysSql = """
        SELECT fk.parent_object_id, fk.object_id, fk.name, pc.name, rs.name, rt.name, rc.name,
               fk.delete_referential_action_desc, fk.update_referential_action_desc, fk.is_disabled
        FROM sys.foreign_keys fk
        JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
        JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
        JOIN sys.tables rt ON rt.object_id = fkc.referenced_object_id
        JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
        JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
        ORDER BY fk.parent_object_id, fk.name, fkc.constraint_column_id
        """;

    private readonly DbContext _dbContext;
    private readonly IEntityDescriptionProvider _descriptionProvider;
    private readonly IMigrationHistoryProvider _migrationHistoryProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SqlServerDatabaseSchemaService> _logger;

    public SqlServerDatabaseSchemaService(
        DbContext dbContext,
        IEntityDescriptionProvider descriptionProvider,
        IMigrationHistoryProvider migrationHistoryProvider,
        TimeProvider timeProvider,
        ILogger<SqlServerDatabaseSchemaService> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(descriptionProvider);
        ArgumentNullException.ThrowIfNull(migrationHistoryProvider);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _dbContext = dbContext;
        _descriptionProvider = descriptionProvider;
        _migrationHistoryProvider = migrationHistoryProvider;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<DatabaseSchemaDto> GetSchemaAsync(SchemaFilterRequest filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        string databaseName;
        string serverVersion;
        List<string> allSchemas;
        List<TableSchemaDto> result;

        await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var connection = _dbContext.Database.GetDbConnection();

            databaseName = connection.Database;
            serverVersion = connection.ServerVersion;
            var supportsLedger = await ScalarAsync<int>(connection,
                "SELECT CASE WHEN COL_LENGTH('sys.tables', 'ledger_type') IS NULL THEN 0 ELSE 1 END", cancellationToken) == 1;

            var tables = await ReadTablesAsync(connection, supportsLedger, cancellationToken);
            var columns = await ReadColumnsAsync(connection, cancellationToken);
            var indexes = await ReadIndexesAsync(connection, cancellationToken);
            var foreignKeys = await ReadForeignKeysAsync(connection, cancellationToken);

            allSchemas = tables.Select(t => t.Schema).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
            result = Assemble(tables, columns, indexes, foreignKeys, _descriptionProvider.GetDescriptions(), filter);

            _logger.LogInformation("讀取資料庫結構完成。Database: {Database}, Tables: {Count}", databaseName, result.Count);
        }
        finally
        {
            await _dbContext.Database.CloseConnectionAsync();
        }

        // 更新履歷：各 DbContext 自行連線讀取 __EFMigrationsHistory；指定 Schema 時僅保留對應 DbContext
        var schemaFilter = ToSet(filter.Schemas);
        var migrations = (await _migrationHistoryProvider.GetHistoryAsync(cancellationToken))
            .Where(m => schemaFilter is null || m.Schema is null || schemaFilter.Contains(m.Schema))
            .ToList();

        return new DatabaseSchemaDto(databaseName, serverVersion, _timeProvider.GetLocalNow(), allSchemas, result, migrations);
    }

    private static List<TableSchemaDto> Assemble(
        List<TableRow> tables,
        Dictionary<int, List<ColumnRow>> columns,
        Dictionary<int, List<IndexSchemaDto>> indexes,
        Dictionary<int, List<ForeignKeySchemaDto>> foreignKeys,
        IReadOnlyDictionary<string, EntityDescriptions> modelDescriptions,
        SchemaFilterRequest filter)
    {
        var schemaFilter = ToSet(filter.Schemas);
        var tableFilter = ToSet(filter.Tables);

        var result = new List<TableSchemaDto>();
        foreach (var table in tables.OrderBy(t => t.Schema, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (table.IsDroppedLedger)
            {
                continue;
            }

            // temporal_type 1 = HISTORY_TABLE；ledger_type 1 = HISTORY_TABLE
            if (!filter.IncludeHistoryTables && (table.TemporalTypeCode == 1 || table.LedgerTypeCode == 1))
            {
                continue;
            }

            if (schemaFilter is not null && !schemaFilter.Contains(table.Schema))
            {
                continue;
            }

            if (tableFilter is not null && !tableFilter.Contains($"{table.Schema}.{table.Name}"))
            {
                continue;
            }

            // 說明優先序：資料庫 MS_Description > EF Core [Comment] / HasComment > [DisplayName] > 系統資料表內建說明
            var modelDescription = modelDescriptions.GetValueOrDefault($"{table.Schema}.{table.Name}")
                ?? SystemTableDescriptions.Find(table.Name);

            var tableIndexes = indexes.GetValueOrDefault(table.ObjectId) ?? [];
            var tableFks = foreignKeys.GetValueOrDefault(table.ObjectId) ?? [];

            var pkColumns = tableIndexes.Where(i => i.IsPrimaryKey).SelectMany(i => SplitColumns(i.Columns)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var uniqueColumns = tableIndexes
                .Where(i => !i.IsPrimaryKey && i.IsUnique && SplitColumns(i.Columns).Count == 1 && string.IsNullOrEmpty(i.Filter))
                .Select(i => SplitColumns(i.Columns)[0])
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var fkReferences = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var fk in tableFks)
            {
                var from = SplitColumns(fk.Columns);
                var to = SplitColumns(fk.ReferencedColumns);
                for (var i = 0; i < from.Count; i++)
                {
                    var target = $"{fk.ReferencedFullName}.{(i < to.Count ? to[i] : "?")}";
                    fkReferences[from[i]] = fkReferences.TryGetValue(from[i], out var existing) ? $"{existing}; {target}" : target;
                }
            }

            var tableColumns = (columns.GetValueOrDefault(table.ObjectId) ?? [])
                .Select(c => new ColumnSchemaDto(
                    c.Ordinal,
                    c.Name,
                    c.TypeName,
                    FormatDataType(c.TypeName, c.MaxLength, c.Precision, c.Scale),
                    c.MaxLength,
                    c.Precision,
                    c.Scale,
                    c.IsNullable,
                    pkColumns.Contains(c.Name),
                    fkReferences.ContainsKey(c.Name),
                    uniqueColumns.Contains(c.Name),
                    c.IsIdentity,
                    c.IsIdentity ? $"IDENTITY({c.IdentitySeed},{c.IdentityIncrement})" : null,
                    c.IsComputed,
                    c.ComputedDefinition,
                    c.DefaultValue,
                    c.Collation,
                    fkReferences.GetValueOrDefault(c.Name),
                    Coalesce(c.Description, modelDescription?.ColumnDescriptions.GetValueOrDefault(c.Name) ?? SystemTableDescriptions.FindColumn(c.Name))))
                .ToList();

            result.Add(new TableSchemaDto(
                table.ObjectId,
                table.Schema,
                table.Name,
                Coalesce(table.Description, modelDescription?.TableDescription),
                table.CreatedAt,
                table.ModifiedAt,
                table.RowCount,
                table.TemporalTypeCode is null or 0 ? null : table.TemporalType,
                table.HistoryTable,
                table.LedgerTypeCode is null or 0 ? null : table.LedgerType,
                tableColumns,
                tableIndexes,
                tableFks));
        }

        return result;
    }

    /// <summary>
    /// 依 SQL Server 型別規則組出完整型別字串，例如 nvarchar(50)、decimal(18,2)、varbinary(max)
    /// </summary>
    internal static string FormatDataType(string typeName, short maxLength, byte precision, byte scale)
    {
        var type = typeName.ToLowerInvariant();
        return type switch
        {
            "varchar" or "char" or "varbinary" or "binary" => $"{typeName}({(maxLength == -1 ? "max" : maxLength.ToString())})",
            "nvarchar" or "nchar" => $"{typeName}({(maxLength == -1 ? "max" : (maxLength / 2).ToString())})",
            "decimal" or "numeric" => $"{typeName}({precision},{scale})",
            "datetime2" or "time" or "datetimeoffset" => $"{typeName}({scale})",
            "float" when precision != 53 => $"{typeName}({precision})",
            _ => typeName
        };
    }

    private static string? Coalesce(string? primary, string? fallback) =>
        string.IsNullOrWhiteSpace(primary) ? fallback : primary;

    private static List<string> SplitColumns(string columns) =>
        columns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.EndsWith(" DESC", StringComparison.Ordinal) ? c[..^5] : c)
            .ToList();

    private static HashSet<string>? ToSet(string[]? values)
    {
        var items = values?
            .SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return items is { Count: > 0 } ? items : null;
    }

    private static async Task<List<TableRow>> ReadTablesAsync(DbConnection connection, bool supportsLedger, CancellationToken cancellationToken)
    {
        var sql = string.Format(TablesSql, supportsLedger ? LedgerColumnsSql : NoLedgerColumnsSql);
        var rows = new List<TableRow>();
        await using var command = CreateCommand(connection, sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new TableRow(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                NullableString(reader, 3),
                reader.GetDateTime(4),
                reader.GetDateTime(5),
                Convert.ToInt64(reader.GetValue(6)),
                reader.IsDBNull(7) ? null : Convert.ToInt32(reader.GetValue(7)),
                NullableString(reader, 8),
                NullableString(reader, 9),
                reader.IsDBNull(10) ? null : Convert.ToInt32(reader.GetValue(10)),
                NullableString(reader, 11),
                !reader.IsDBNull(12) && reader.GetBoolean(12)));
        }

        return rows;
    }

    private static async Task<Dictionary<int, List<ColumnRow>>> ReadColumnsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, List<ColumnRow>>();
        await using var command = CreateCommand(connection, ColumnsSql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new ColumnRow(
                reader.GetInt32(0),
                reader.GetInt32(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt16(4),
                reader.GetByte(5),
                reader.GetByte(6),
                reader.GetBoolean(7),
                reader.GetBoolean(8),
                NullableString(reader, 9),
                NullableString(reader, 10),
                reader.GetBoolean(11),
                NullableString(reader, 12),
                NullableString(reader, 13),
                NullableString(reader, 14),
                NullableString(reader, 15));
            AddTo(result, row.ObjectId, row);
        }

        return result;
    }

    private static async Task<Dictionary<int, List<IndexSchemaDto>>> ReadIndexesAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var raw = new List<(int ObjectId, int IndexId, string Name, string Type, bool IsPk, bool IsUnique, bool IsUc, string? Filter, string Column, bool IsIncluded, bool IsDesc)>();
        await using (var command = CreateCommand(connection, IndexesSql))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                raw.Add((reader.GetInt32(0), reader.GetInt32(1), NullableString(reader, 2) ?? string.Empty, reader.GetString(3),
                    reader.GetBoolean(4), reader.GetBoolean(5), reader.GetBoolean(6), NullableString(reader, 7),
                    reader.GetString(8), reader.GetBoolean(9), reader.GetBoolean(10)));
            }
        }

        var result = new Dictionary<int, List<IndexSchemaDto>>();
        foreach (var group in raw.GroupBy(r => (r.ObjectId, r.IndexId)))
        {
            var first = group.First();
            var keys = string.Join(", ", group.Where(r => !r.IsIncluded).Select(r => r.IsDesc ? $"{r.Column} DESC" : r.Column));
            var included = string.Join(", ", group.Where(r => r.IsIncluded).Select(r => r.Column));
            AddTo(result, first.ObjectId, new IndexSchemaDto(
                first.Name,
                first.Type,
                first.IsPk,
                first.IsUnique,
                first.IsUc,
                keys,
                included.Length == 0 ? null : included,
                first.Filter));
        }

        return result;
    }

    private static async Task<Dictionary<int, List<ForeignKeySchemaDto>>> ReadForeignKeysAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var raw = new List<(int ParentId, int FkId, string Name, string Column, string RefSchema, string RefTable, string RefColumn, string OnDelete, string OnUpdate, bool IsDisabled)>();
        await using (var command = CreateCommand(connection, ForeignKeysSql))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                raw.Add((reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                    reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetString(8), reader.GetBoolean(9)));
            }
        }

        var result = new Dictionary<int, List<ForeignKeySchemaDto>>();
        foreach (var group in raw.GroupBy(r => (r.ParentId, r.FkId)))
        {
            var first = group.First();
            AddTo(result, first.ParentId, new ForeignKeySchemaDto(
                first.Name,
                string.Join(", ", group.Select(r => r.Column)),
                first.RefSchema,
                first.RefTable,
                string.Join(", ", group.Select(r => r.RefColumn)),
                first.OnDelete.Replace('_', ' '),
                first.OnUpdate.Replace('_', ' '),
                first.IsDisabled));
        }

        return result;
    }

    private static async Task<T> ScalarAsync<T>(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, sql);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return (T)Convert.ChangeType(value!, typeof(T));
    }

    private static DbCommand CreateCommand(DbConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 120;
        return command;
    }

    private static string? NullableString(DbDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));

    private static void AddTo<T>(Dictionary<int, List<T>> map, int key, T value)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = [];
            map[key] = list;
        }

        list.Add(value);
    }

    private sealed record TableRow(
        int ObjectId,
        string Schema,
        string Name,
        string? Description,
        DateTime CreatedAt,
        DateTime ModifiedAt,
        long RowCount,
        int? TemporalTypeCode,
        string? TemporalType,
        string? HistoryTable,
        int? LedgerTypeCode,
        string? LedgerType,
        bool IsDroppedLedger);

    private sealed record ColumnRow(
        int ObjectId,
        int Ordinal,
        string Name,
        string TypeName,
        short MaxLength,
        byte Precision,
        byte Scale,
        bool IsNullable,
        bool IsIdentity,
        string? IdentitySeed,
        string? IdentityIncrement,
        bool IsComputed,
        string? ComputedDefinition,
        string? DefaultValue,
        string? Collation,
        string? Description);
}
