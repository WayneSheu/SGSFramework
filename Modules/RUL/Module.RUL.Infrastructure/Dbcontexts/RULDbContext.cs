using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using SGS.Modules.RUL.Infrastructure.Entities;
using SGS.Modules.RUL.Infrastructure.Migrations;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Persistent.Abstractions.Dbcontexts;

namespace SGS.Modules.RUL.Infrastructure.Dbcontexts;

/// <summary>
/// RUL 動態規則引擎模組專屬 DbContext（schema: rul）：規則、變更申請（Maker-Checker）、版本與測試案例（皆為 IAuditable，異動寫入稽核）。
/// </summary>
public class RULDbContext : BaseDbContext<RULDbContext>
{
    public RULDbContext(DbContextOptions<RULDbContext> options)
        : base(options)
    {
    }

    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    public DbSet<RuleDefinition> Rules { get; set; } = null!;

    public DbSet<RuleChangeRequest> ChangeRequests { get; set; } = null!;

    public DbSet<RuleVersion> Versions { get; set; } = null!;

    public DbSet<RuleTestCase> TestCases { get; set; } = null!;


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

        modelBuilder.HasDefaultSchema("rul");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RULDbContext).Assembly);

        modelBuilder.Entity<OutboxMessage>(b => b.ToTable("OutboxMessages", "rul"));
    }
}
