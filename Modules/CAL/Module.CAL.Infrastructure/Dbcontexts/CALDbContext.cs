using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using SGS.Modules.CAL.Infrastructure.Entities;
using SGS.Modules.CAL.Infrastructure.Migrations;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Persistent.Abstractions.Dbcontexts;

namespace SGS.Modules.CAL.Infrastructure.Dbcontexts;

/// <summary>
/// CAL 行事曆管理模組專屬 DbContext（schema: cal）：假日與補班日、日曆與活動（皆為 IAuditable，異動寫入稽核）。
/// </summary>
public class CALDbContext : BaseDbContext<CALDbContext>
{
    public CALDbContext(DbContextOptions<CALDbContext> options)
        : base(options)
    {
    }

    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    public DbSet<Holiday> Holidays { get; set; } = null!;

    public DbSet<Calendar> Calendars { get; set; } = null!;

    public DbSet<CalendarEvent> CalendarEvents { get; set; } = null!;

    public DbSet<CalendarEventAttendee> CalendarEventAttendees { get; set; } = null!;

    public DbSet<CalendarEventReminder> CalendarEventReminders { get; set; } = null!;

    public DbSet<HolidaySyncRun> HolidaySyncRuns { get; set; } = null!;

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

        modelBuilder.HasDefaultSchema("cal");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CALDbContext).Assembly);

        modelBuilder.Entity<OutboxMessage>(b => b.ToTable("OutboxMessages", "cal"));
    }
}
