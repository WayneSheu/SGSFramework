using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.SoftDelet;
using SGSFramework.Core.Abstractions.Entities.Tenants;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Core.Identiies.Tenants;
using SGSFramework.Persistent.Configurations;
using SGSFramework.Persistent.Extensions;
using SGSFramework.Persistent.Helpers;

namespace SGSFramework.Persistent.Abstractions.Dbcontexts;

/// <summary>
/// 企業級泛型 DbContext 基底類別
/// 整合多租戶、自動審計、軟刪除、HierarchyId、[Module] Schema 解析與 OutboxRepository
/// </summary>
public abstract class BaseDbContext<TContext> : DbContext, IOutboxRepository
    where TContext : DbContext
{
    private readonly string? _tenantId;
    protected string _schema { get; }

    protected BaseDbContext(DbContextOptions<TContext> options, ITenantService? tenantService = null)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // 1. 優先從 Assembly 或 Context 類別嘗試讀取 [Module] Attribute 的 ModuleCode
        var moduleAttr = typeof(TContext).Assembly.GetCustomAttribute<ModuleAttribute>()
                         ?? typeof(TContext).GetCustomAttribute<ModuleAttribute>();

        if (moduleAttr != null && !string.IsNullOrWhiteSpace(moduleAttr.ModuleCode))
        {
            _schema = moduleAttr.ModuleCode;
        }
        else
        {
            // 2. 次之從 Options 的 ToolkitOptionsExtension 讀取
            var extension = options.FindExtension<ToolkitOptionsExtension>();
            _schema = extension?.Schema ?? "core";
        }

        // 取得目前租戶 ID (執行期由 DI 注入)
        _tenantId = tenantService?.GetTenantId();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);

        // 套用全域慣例與轉換政策
        configurationBuilder.ApplySESDefaultConventions();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        base.OnConfiguring(optionsBuilder);

        // 僅於本地開發環境啟用敏感資料記錄
        if (string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
        }
    }

    #region IOutboxRepository 實作

    /// <summary>
    /// 使用 SQL Server 特有的 UPDATE...OUTPUT 語法實現原子化抓取。
    /// 配合 ROWLOCK 與 READPAST 提示，確保多個 Background Worker 之間不競爭同一筆資料。
    /// </summary>
    public async Task<List<OutboxMessage>> FetchOutboxMessagesAsync(int batchSize)
    {
        if (batchSize <= 0)
        {
            return new List<OutboxMessage>();
        }

        var processingFetchTime = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var entityType = Model.FindEntityType(typeof(OutboxMessage));
        var tableName = entityType?.GetTableName() ?? "OutboxMessages";

        // 於此轉譯 Schema 名稱
        var resolvedSchema = (entityType?.GetSchema() ?? _schema).ToLowerInvariant();

        var sql = $@"
            UPDATE TOP (@batchSize) [{resolvedSchema}].[{tableName}] WITH (ROWLOCK, READPAST)
            SET [{nameof(OutboxMessage.ProcessedOnUtc)}] = @fetchTime
            OUTPUT INSERTED.*
            WHERE [{nameof(OutboxMessage.ProcessedOnUtc)}] IS NULL
            AND ([{nameof(OutboxMessage.ScheduledAtUtc)}] IS NULL OR [{nameof(OutboxMessage.ScheduledAtUtc)}] <= GETUTCDATE())
            AND [{nameof(OutboxMessage.IsDead)}] = 0";

        var batchSizeParam = new SqlParameter("@batchSize", batchSize);
        var fetchTimeParam = new SqlParameter("@fetchTime", processingFetchTime);

        return await Set<OutboxMessage>()
            .FromSqlRaw(sql, batchSizeParam, fetchTimeParam)
            .IgnoreQueryFilters()
            .ToListAsync();
    }

    #endregion

    /// <summary>
    /// 配置模型建立邏輯
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        // 將 ModuleCode 轉為小寫做為預設 DB Schema
        string normalizedSchema = _schema.ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(normalizedSchema))
        {
            modelBuilder.HasDefaultSchema(normalizedSchema);
        }

        // 自動套用當前 Context Assembly 的 IEntityTypeConfiguration
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TContext).Assembly);

        // 針對所有實體動態調整 Schema
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.GetViewName() != null) continue;

            var tableName = entityType.GetTableName();
            if (!string.IsNullOrEmpty(tableName))
            {
                entityType.SetSchema(normalizedSchema);
            }
        }

        // 套用通用的 Outbox、Log 與多租戶配置
        DbConfigurationHelper.ApplyCommonConfigs(modelBuilder, normalizedSchema, typeof(TContext), _tenantId);

        // 套用軟刪除過濾器
        modelBuilder.ApplySoftDeleteFilters();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 自動為實作 ITenantEntity 的實體設置 TenantID 陰影屬性值
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
        {
            if (entry.Entity is ITenantEntity)
            {
                var tenantProperty = entry.Metadata.FindProperty("TenantID");
                if (tenantProperty != null)
                {
                    entry.Property("TenantID").CurrentValue = _tenantId;
                }
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}