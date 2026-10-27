using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SGSFramework.ApiInfrastructure.DTOs.DatabaseSchemas;
using SGSFramework.AuditLog.Configurations;
using SGSFramework.Core.Mask;
using SGSFramework.Core.Security.FieldEncryption;

namespace SGSFramework.ApiInfrastructure.Services.DatabaseSchemas;

/// <summary>
/// 資料表 / 欄位說明與資安保護標註 (取自 EF Core 模型)
/// </summary>
public sealed record EntityDescriptions(
    string? TableDescription,
    IReadOnlyDictionary<string, string> ColumnDescriptions,
    IReadOnlyDictionary<string, ColumnProtectionDto>? ColumnProtections = null);

/// <summary>
/// 自所有已註冊 DbContext 的 EF Core 設計階段模型讀取 [Comment] / HasComment 與 [DisplayName]，
/// 作為資料庫尚未寫入 MS_Description (未產生 Migration) 時的說明來源；
/// 並彙整欄位的資安保護標註 ([Encrypted]、[Masked]、[AuditIgnore] 與稽核組態)。
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
    // 外掛模組於獨立 AssemblyLoadContext 載入，可能持有另一份 Core 型別：以完整型別名稱比對，而非型別同一性
    private const string EncryptedAttributeName = "SGSFramework.Core.Abstractions.Attributes.EncryptedAttribute";
    private const string SensitiveDataAttributeName = "SGSFramework.Core.Abstractions.Attributes.SensitiveDataAttribute";
    private const string AuditIgnoreAttributeName = "SGSFramework.Core.Abstractions.Attributes.AuditIgnoreAttribute";
    private const string AuditableInterfaceName = "SGSFramework.Core.Abstractions.Entities.AuditLogs.IAuditable";

    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<AuditOptions> _auditOptions;
    private readonly ILogger<EfModelDescriptionProvider> _logger;

    public EfModelDescriptionProvider(
        IServiceProvider serviceProvider,
        IOptionsMonitor<AuditOptions> auditOptions,
        ILogger<EfModelDescriptionProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        ArgumentNullException.ThrowIfNull(auditOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _serviceProvider = serviceProvider;
        _auditOptions = auditOptions;
        _logger = logger;
    }

    private sealed class TableEntry
    {
        public string? Table { get; set; }
        public Dictionary<string, string> Columns { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, ColumnProtectionDto> Protections { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, EntityDescriptions> GetDescriptions()
    {
        var tables = new Dictionary<string, TableEntry>(StringComparer.OrdinalIgnoreCase);
        var audit = _auditOptions.CurrentValue;

        foreach (var context in DbContextDiscovery.ResolveRegistered(_serviceProvider, _logger))
        {
            try
            {
                // Comment 屬於設計階段註記，Runtime Model 會被移除，需取 Design-Time Model
                var model = context.GetService<IDesignTimeModel>().Model;
                Collect(model, tables, audit);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "讀取 DbContext {ContextType} 的 EF 模型說明失敗。", context.GetType().Name);
            }
        }

        return tables.ToDictionary(
            kv => kv.Key,
            kv => new EntityDescriptions(kv.Value.Table, kv.Value.Columns, kv.Value.Protections),
            StringComparer.OrdinalIgnoreCase);
    }

    private static void Collect(IModel model, Dictionary<string, TableEntry> tables, AuditOptions audit)
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
                entry = new TableEntry();
                tables[key] = entry;
            }

            // 共用資料表 (TPH / Owned) 時以根實體的說明為主
            var tableDescription = entityType.GetComment() ?? DisplayNameOf(entityType.ClrType);
            if (entry.Table is null && !string.IsNullOrWhiteSpace(tableDescription) && entityType.BaseType is null)
            {
                entry.Table = tableDescription;
            }

            // 與 AuditInterceptor 一致：只有實作 IAuditable 的實體會寫入資料日誌
            var isAudited = entityType.ClrType.GetInterfaces().Any(i => i.FullName == AuditableInterfaceName);

            var storeObject = StoreObjectIdentifier.Table(tableName, entityType.GetSchema());
            foreach (var property in entityType.GetProperties())
            {
                var columnName = property.GetColumnName(storeObject);
                if (columnName is null)
                {
                    continue;
                }

                if (!entry.Columns.ContainsKey(columnName))
                {
                    var description = property.GetComment() ?? DisplayNameOf(property.PropertyInfo);
                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        entry.Columns[columnName] = description;
                    }
                }

                if (!entry.Protections.ContainsKey(columnName)
                    && ResolveProtection(entityType.ClrType, property, isAudited, audit) is { } protection)
                {
                    entry.Protections[columnName] = protection;
                }
            }
        }
    }

    /// <summary>
    /// 彙整欄位的資安保護；規則與實際執行的元件一致 (FieldEncryptor、SensitiveDataDestructuringPolicy、AuditInterceptor)
    /// </summary>
    private static ColumnProtectionDto? ResolveProtection(Type clrType, IReadOnlyProperty property, bool isAudited, AuditOptions audit)
    {
        var member = (MemberInfo?)property.PropertyInfo ?? property.FieldInfo;
        var name = property.Name;

        var isEncrypted = property.IsEncryptedProperty() || FindAttribute(member, EncryptedAttributeName) is not null;
        var maskFormat = MaskFormatOf(FindAttribute(member, SensitiveDataAttributeName));

        string? auditHandling = null;
        if (isAudited && !property.IsPrimaryKey())
        {
            if (audit.NoisyProperties.Contains(name, StringComparer.OrdinalIgnoreCase) || IsAuditIgnored(clrType, name))
            {
                auditHandling = "Ignored";
            }
            else if (isEncrypted || audit.ExcludedProperties.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                auditHandling = "Masked";
            }
        }

        return isEncrypted || maskFormat is not null || auditHandling is not null
            ? new ColumnProtectionDto(isEncrypted, maskFormat, auditHandling)
            : null;
    }

    /// <summary>
    /// [AuditIgnore] 可標在屬性本身，或標在 IAuditable 等介面成員上 (與 AuditInterceptor 判斷方式相同)
    /// </summary>
    private static bool IsAuditIgnored(Type clrType, string propertyName) =>
        FindAttribute(clrType.GetProperty(propertyName), AuditIgnoreAttributeName) is not null
        || clrType.GetInterfaces().Any(i => FindAttribute(i.GetProperty(propertyName), AuditIgnoreAttributeName) is not null);

    private static Attribute? FindAttribute(MemberInfo? member, string attributeTypeName) =>
        member?.GetCustomAttributes(inherit: true).OfType<Attribute>().FirstOrDefault(a => IsOrDerivesFrom(a.GetType(), attributeTypeName));

    private static bool IsOrDerivesFrom(Type? type, string fullName)
    {
        for (; type is not null; type = type.BaseType)
        {
            if (type.FullName == fullName) return true;
        }
        return false;
    }

    /// <summary>
    /// 讀取 SensitiveDataAttribute.Format 並轉為顯示名稱 (例如「姓名遮蔽 (陳○明)」)
    /// </summary>
    private static string? MaskFormatOf(Attribute? sensitiveAttribute)
    {
        if (sensitiveAttribute is null) return null;

        var formatName = sensitiveAttribute.GetType().GetProperty("Format")?.GetValue(sensitiveAttribute)?.ToString();
        if (formatName is null || !Enum.TryParse<MaskFormat>(formatName, out var format))
        {
            return formatName ?? "預設遮蔽";
        }

        return typeof(MaskFormat).GetField(format.ToString())?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? format.ToString();
    }

    /// <summary>
    /// 顯示名稱：[DisplayName] 優先，其次為 DataAnnotations 的 [Display(Name = ...)]
    /// </summary>
    private static string? DisplayNameOf(MemberInfo? member) =>
        member?.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName
        ?? member?.GetCustomAttribute<DisplayAttribute>()?.GetName();
}
