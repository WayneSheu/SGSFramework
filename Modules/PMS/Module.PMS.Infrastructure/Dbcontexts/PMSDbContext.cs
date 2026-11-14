using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using SGS.Modules.PMS.Infrastructure.Entities;
using SGS.Modules.PMS.Infrastructure.Migrations;
using SGSFramework.Core.Abstractions.DbContexts;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Persistent.Abstractions.Dbcontexts;

namespace SGS.Modules.PMS.Infrastructure.Dbcontexts;

/// <summary>
/// PMS 專案管理模組專屬 DbContext（schema: pms）：專案、成員、Gate、交付物與專案代號保留表（IAuditable）。
/// </summary>
public class PMSDbContext : BaseDbContext<PMSDbContext>
{
    public PMSDbContext(DbContextOptions<PMSDbContext> options)
        : base(options)
    {
    }

    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    public DbSet<Project> Projects { get; set; } = null!;

    public DbSet<ProjectMember> ProjectMembers { get; set; } = null!;

    public DbSet<PmsModule> Modules { get; set; } = null!;

    public DbSet<ProjectGate> ProjectGates { get; set; } = null!;

    public DbSet<Deliverable> Deliverables { get; set; } = null!;

    public DbSet<ProjectCodeReservation> ProjectCodeReservations { get; set; } = null!;

    public DbSet<TicketSequence> TicketSequences { get; set; } = null!;

    public DbSet<Ticket> Tickets { get; set; } = null!;

    public DbSet<TicketEvent> TicketEvents { get; set; } = null!;

    public DbSet<TicketAttachment> TicketAttachments { get; set; } = null!;

    public DbSet<Holiday> Holidays { get; set; } = null!;

    public DbSet<ChangeRequestSequence> ChangeRequestSequences { get; set; } = null!;

    public DbSet<ChangeRequest> ChangeRequests { get; set; } = null!;

    public DbSet<CrImpactAssessment> CrImpactAssessments { get; set; } = null!;

    public DbSet<CrSignoff> CrSignoffs { get; set; } = null!;

    public DbSet<CrEvent> CrEvents { get; set; } = null!;
    public DbSet<ReleaseChecklist> ReleaseChecklists { get; set; } = null!;
    public DbSet<ReleaseChecklistItem> ReleaseChecklistItems { get; set; } = null!;
    public DbSet<ReleaseSignoff> ReleaseSignoffs { get; set; } = null!;
    public DbSet<RcaReport> RcaReports { get; set; } = null!;
    public DbSet<UatSignoff> UatSignoffs { get; set; } = null!;
    public DbSet<UatAttachment> UatAttachments { get; set; } = null!;
    public DbSet<MonthlyReport> MonthlyReports { get; set; } = null!;
    public DbSet<RiskItem> RiskItems { get; set; } = null!;
    public DbSet<ProjectTask> ProjectTasks { get; set; } = null!;
    public DbSet<ProjectTaskDependency> ProjectTaskDependencies { get; set; } = null!;
    public DbSet<ProjectBaseline> ProjectBaselines { get; set; } = null!;
    public DbSet<ProjectBaselineTask> ProjectBaselineTasks { get; set; } = null!;

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

        modelBuilder.HasDefaultSchema("pms");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PMSDbContext).Assembly);

        modelBuilder.Entity<OutboxMessage>(b => b.ToTable("OutboxMessages", "pms"));
    }
}
