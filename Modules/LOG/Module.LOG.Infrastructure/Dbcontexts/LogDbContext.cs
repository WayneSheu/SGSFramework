using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using SGSFramework.Core.Migrations;
using SGS.Modules.LOG.Infrastructure.Migrations;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Abstractions.Logings;
using SGSFramework.Persistent.Abstractions.Dbcontexts;

namespace SGS.Modules.LOG.Infrastructure.Dbcontexts;

/// <summary>
/// LOG 日誌模組專屬 DbContext（獨立資料庫、schema: log）：系統日誌、資安日誌、資料日誌三張 SQL Ledger APPEND_ONLY 資料表。
/// 刻意不實作 IAuditDbContext、不掛 AuditInterceptor：日誌庫本身不再產生資料稽核（避免稽核自己）。
/// </summary>
public class LogDbContext : BaseDbContext<LogDbContext>
{
    public LogDbContext(DbContextOptions<LogDbContext> options)
        : base(options)
    {
    }

    public DbSet<SystemLog> SystemLogs { get; set; } = null!;

    public DbSet<SecurityLog> SecurityLogs { get; set; } = null!;

    public DbSet<AuditLogEntity> AuditLogs { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        base.OnConfiguring(optionsBuilder);

        optionsBuilder.ReplaceService<IMigrationsAssembly, PluginMigrationsAssembly>();

        // 三張日誌表須為 SQL Ledger APPEND_ONLY：套用 Core 的自訂 Annotation Provider 與 SQL 產生器（建表時附加 WITH (LEDGER = ON ...)）
        optionsBuilder.ReplaceService<IRelationalAnnotationProvider, CustomSqlServerAnnotationProvider>();
        optionsBuilder.ReplaceService<IMigrationsSqlGenerator, CustomSqlServerMigrationsSqlGenerator>();

        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("log");

        // 只套用三張日誌表的 Core 組態（不可 ApplyConfigurationsFromAssembly(Core)，會把整個主庫模型拉進來）
        modelBuilder.ApplyConfiguration(new SystemLogConfiguration());
        modelBuilder.ApplyConfiguration(new SecurityAuditLedgerConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());

        modelBuilder.Entity<SystemLog>(b => b.ToTable("SystemLogs", "log"));
        modelBuilder.Entity<SecurityLog>(b => b.ToTable("SecurityLog", "log"));
        modelBuilder.Entity<AuditLogEntity>(b =>
        {
            b.ToTable("AuditLogs", "log");
            // 雜湊鏈以 Schema（core/org/fms/rpt/sch…）分鏈，查上一筆雜湊需此索引
            b.HasIndex(e => new { e.Schema, e.Id }).HasDatabaseName("IX_AuditLog_Schema_Id");
        });
    }
}
