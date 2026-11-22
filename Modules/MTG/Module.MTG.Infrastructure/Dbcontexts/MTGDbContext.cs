using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using SGS.Modules.MTG.Infrastructure.Entities;
using SGS.Modules.MTG.Infrastructure.Migrations;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Persistent.Abstractions.Dbcontexts;

namespace SGS.Modules.MTG.Infrastructure.Dbcontexts;

/// <summary>
/// MTG 會議助理模組專屬 DbContext（schema: mtg）：會議、逐字稿、紀要版本、場景模板與術語表。
/// </summary>
public class MTGDbContext : BaseDbContext<MTGDbContext>
{
    public MTGDbContext(DbContextOptions<MTGDbContext> options)
        : base(options)
    {
    }

    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    public DbSet<Meeting> Meetings { get; set; } = null!;

    public DbSet<TranscriptSegment> TranscriptSegments { get; set; } = null!;

    public DbSet<MeetingAudio> MeetingAudios { get; set; } = null!;

    public DbSet<MeetingMinutes> MeetingMinutes { get; set; } = null!;

    public DbSet<PromptTemplate> PromptTemplates { get; set; } = null!;

    public DbSet<GlossaryEntry> GlossaryEntries { get; set; } = null!;

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

        modelBuilder.HasDefaultSchema("mtg");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MTGDbContext).Assembly);

        modelBuilder.Entity<OutboxMessage>(b => b.ToTable("OutboxMessages", "mtg"));
    }
}
