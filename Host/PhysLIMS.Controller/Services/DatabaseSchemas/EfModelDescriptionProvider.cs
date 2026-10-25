using System.ComponentModel;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace SGSFramework.ApiInfrastructure.Services.DatabaseSchemas;

/// <summary>
/// 資料表 / 欄位說明 (取自 EF Core 模型)
/// </summary>
public sealed record EntityDescriptions(string? TableDescription, IReadOnlyDictionary<string, string> ColumnDescriptions);

/// <summary>
/// 自所有已註冊 DbContext 的 EF Core 設計階段模型讀取 [Comment] / HasComment 與 [DisplayName]，
/// 作為資料庫尚未寫入 MS_Description (未產生 Migration) 時的說明來源。
/// </summary>
public interface IEntityDescriptionProvider
{
    /// <summary>
    /// 以 schema.table (不分大小寫) 為鍵的說明對照表
    /// </summary>
    IReadOnlyDictionary<string, EntityDescriptions> GetDescriptions();
}

public sealed class EfModelDescriptionProvider : IEntityDescriptionProvider
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EfModelDescriptionProvider> _logger;

    public EfModelDescriptionProvider(IServiceProvider serviceProvider, ILogger<EfModelDescriptionProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public IReadOnlyDictionary<string, EntityDescriptions> GetDescriptions()
    {
        var tables = new Dictionary<string, (string? Table, Dictionary<string, string> Columns)>(StringComparer.OrdinalIgnoreCase);

        foreach (var context in DbContextDiscovery.ResolveRegistered(_serviceProvider, _logger))
        {
            try
            {
                // Comment 屬於設計階段註記，Runtime Model 會被移除，需取 Design-Time Model
                var model = context.GetService<IDesignTimeModel>().Model;
                Collect(model, tables);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "讀取 DbContext {ContextType} 的 EF 模型說明失敗。", context.GetType().Name);
            }
        }

        return tables.ToDictionary(
            kv => kv.Key,
            kv => new EntityDescriptions(kv.Value.Table, kv.Value.Columns),
            StringComparer.OrdinalIgnoreCase);
    }

    private static void Collect(IModel model, Dictionary<string, (string? Table, Dictionary<string, string> Columns)> tables)
    {
        foreach (var entityType in model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (tableName is null)
            {
                continue;
            }

            var schema = entityType.GetSchema() ?? "dbo";
            var key = $"{schema}.{tableName}";
            if (!tables.TryGetValue(key, out var entry))
            {
                entry = (null, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            }

            // 共用資料表 (TPH / Owned) 時以根實體的說明為主
            var tableDescription = entityType.GetComment() ?? DisplayNameOf(entityType.ClrType);
            if (entry.Table is null && !string.IsNullOrWhiteSpace(tableDescription) && entityType.BaseType is null)
            {
                entry.Table = tableDescription;
            }

            var storeObject = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName(storeObject);
                if (columnName is null || entry.Columns.ContainsKey(columnName))
                {
                    continue;
                }

                var description = property.GetComment() ?? DisplayNameOf(property.PropertyInfo);
                if (!string.IsNullOrWhiteSpace(description))
                {
                    entry.Columns[columnName] = description;
                }
            }

            tables[key] = entry;
        }
    }

    private static string? DisplayNameOf(MemberInfo? member) =>
        member?.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName;
}
