namespace SGSFramework.Persistent.Abstractions.Dbcontexts;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Entities.Controller;
using SGSFramework.Core.Abstractions.Entities.Identities;
using SGSFramework.Core.Abstractions.Entities.Tenants;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Core.Identiies.Tenants;
using SGSFramework.Persistent.Configurations;
using SGSFramework.Persistent.Extensions;
using SGSFramework.Persistent.Helpers;

/// <summary>
/// 企業級泛型 DbContext 基底類別
/// 整合了 Identity, 多租戶, 自動審計, 軟刪除, OutboxRepository 與 AuthTokenBucket 聯防核心
/// </summary>
public abstract class BaseIdentityDbContext<TUser, TRole, TKey, TContext>
   : IdentityDbContext<TUser, TRole, TKey>, IOutboxRepository, ITokenDbContext
   where TUser : IdentityUser<TKey>
   where TRole : IdentityRole<TKey>
   where TKey : IEquatable<TKey>
   where TContext : DbContext
{
    private readonly string? _schema;
    private readonly string? _tenantId;

    protected BaseIdentityDbContext(
       DbContextOptions<TContext> options,
       ITenantService? tenantService = null) : base(options)
    {
        var extension = options.FindExtension<ToolkitOptionsExtension>();
        _schema = extension?.Schema ?? "core";
        _tenantId = tenantService?.GetTenantId();
    }

    #region 🚀 ITokenDbContext 實作 (AuthTokenBucket 與動態選單快取載體)

    /// <summary>
    /// 泛型套件專用之多裝置安全換票與熔斷機制快取表
    /// </summary>
    public DbSet<UserRefreshToken> UserRefreshTokens { get; set; } = null!;

    /// <summary>
    /// 身分補償修復憑證（Ticket）資料集
    /// </summary>
    public DbSet<RemediationTicket> RemediationTickets { get; set; } = null!;

    /// <summary>
    /// 動態選單資料集 (解決 CS0535 未實作介面成員之錯誤)
    /// </summary>
    public DbSet<MenuItem> MenuItems { get; set; } = null!;

    #endregion

    #region IOutboxRepository 實作

    public async Task<List<OutboxMessage>> FetchOutboxMessagesAsync(int batchSize)
    {
        var processingFetchTime = new DateTime(1900, 1, 1);
        var entityType = Model.FindEntityType(typeof(OutboxMessage));
        var tableName = entityType?.GetTableName() ?? "OutboxMessages";
        var schema = entityType?.GetSchema() ?? _schema;

        var sql = $@"
            UPDATE TOP (@batchSize) [{schema}].[{tableName}] WITH (ROWLOCK, READPAST)
            SET [{nameof(OutboxMessage.ProcessedOnUtc)}] = @fetchTime
            OUTPUT INSERTED.*
            WHERE [{nameof(OutboxMessage.ProcessedOnUtc)}] IS NULL
            AND ([{nameof(OutboxMessage.ScheduledAtUtc)}] IS NULL OR [{nameof(OutboxMessage.ScheduledAtUtc)}] <= GETUTCDATE())
            AND [{nameof(OutboxMessage.IsDead)}] = 0";

        var batchSizeParam = new Microsoft.Data.SqlClient.SqlParameter("@batchSize", batchSize);
        var fetchTimeParam = new Microsoft.Data.SqlClient.SqlParameter("@fetchTime", processingFetchTime);

        return await Set<OutboxMessage>()
            .FromSqlRaw(sql, batchSizeParam, fetchTimeParam)
            .IgnoreQueryFilters()
            .ToListAsync();
    }

    #endregion

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        // 🛡️ 只有本地開發環境才啟用
        if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
        {
            optionsBuilder.EnableSensitiveDataLogging();
            optionsBuilder.EnableDetailedErrors();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TContext).Assembly);
        modelBuilder.HasDefaultSchema(_schema);

        // 🚀 為 UserRefreshToken 建立高效複合索引，優化 MSSQL 2025 的 UPDLOCK 與併發查詢
        modelBuilder.Entity<UserRefreshToken>(entity =>
        {
            entity.ToTable("UserRefreshTokens", _schema);
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.DeviceId }).IsUnique();
        });

        // 🚀 為 MenuItem 建立動態選單模型結構與階層查詢索引優化
        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.ToTable("MenuItems", _schema);
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.HasIndex(e => e.ParentId);
            entity.HasIndex(e => new { e.IsActive, e.IsVisible, e.DisplayOrder });
        });

        DbConfigurationHelper.ApplyCommonConfigs(modelBuilder, _schema, typeof(TContext), _tenantId);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
        {
            if (entry.Entity is ITenantEntity)
            {
                entry.Property("TenantID").CurrentValue = _tenantId;
            }
        }
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.ApplySESDefaultConventions();
    }
}