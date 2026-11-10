using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using SGS.Modules.SCH.Infrastructure.Entities;
using SGS.Modules.SCH.Infrastructure.Migrations;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Persistent.Abstractions.Dbcontexts;

namespace SGS.Modules.SCH.Infrastructure.Dbcontexts;

/// <summary>
/// SCH 排程模組專屬 DbContext（schema: sch）：排程作業設定（IAuditable，變更寫入 sch.AuditLogs）與執行紀錄。
/// </summary>
public class SCHDbContext : BaseDbContext<SCHDbContext>
{
    public SCHDbContext(DbContextOptions<SCHDbContext> options)
        : base(options)
    {
    }

    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    public DbSet<ScheduledJobSetting> ScheduledJobSettings { get; set; } = null!;

    public DbSet<ScheduledJobRun> ScheduledJobRuns { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        base.OnConfiguring(optionsBuilder);

        // 替換 EF Core 內部的 IMigrationsAssembly 為外掛動態載入強化版
        optionsBuilder.ReplaceService<IMigrationsAssembly, PluginMigrationsAssembly>();

        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("sch");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SCHDbContext).Assembly);

        modelBuilder.Entity<OutboxMessage>(b => b.ToTable("OutboxMessages", "sch"));
    }
}
