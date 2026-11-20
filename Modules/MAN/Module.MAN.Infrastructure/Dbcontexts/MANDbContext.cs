using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using SGS.Modules.MAN.Infrastructure.Entities;
using SGS.Modules.MAN.Infrastructure.Migrations;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Persistent.Abstractions.Dbcontexts;

namespace SGS.Modules.MAN.Infrastructure.Dbcontexts;

/// <summary>MAN 操作手冊管理模組專屬 DbContext（schema: man）：手冊主題（IAuditable，異動寫入稽核）。</summary>
public class MANDbContext : BaseDbContext<MANDbContext>
{
    public MANDbContext(DbContextOptions<MANDbContext> options)
        : base(options)
    {
    }

    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    public DbSet<ManualTopic> ManualTopics { get; set; } = null!;

    public DbSet<GenerationJob> GenerationJobs { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ReplaceService<IMigrationsAssembly, PluginMigrationsAssembly>();
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("man");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MANDbContext).Assembly);
        modelBuilder.Entity<OutboxMessage>(b => b.ToTable("OutboxMessages", "man"));
    }
}
