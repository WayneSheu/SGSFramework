using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using SGS.Modules.QTN.Infrastructure.Entities;
using SGS.Modules.QTN.Infrastructure.Migrations;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Core.Security.FieldEncryption;
using SGSFramework.Persistent.Abstractions.Dbcontexts;

namespace SGS.Modules.QTN.Infrastructure.Dbcontexts;

/// <summary>
/// QTN 報價管理模組專屬 DbContext（schema: qtn）：客戶、角色費率、標準 WBS 工時庫、商機、報價版本與 WBS、審批與稽核軌跡、
/// CR 變更評估、結案復盤（皆為 IAuditable，異動寫入稽核；成本欄位以 [SensitiveData] 遮罩）。
/// </summary>
public class QTNDbContext : BaseDbContext<QTNDbContext>
{
    public QTNDbContext(DbContextOptions<QTNDbContext> options)
        : base(options)
    {
    }

    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<RateRole> RateRoles { get; set; } = null!;
    public DbSet<RateRoleHistory> RateRoleHistories { get; set; } = null!;
    public DbSet<WbsTemplate> WbsTemplates { get; set; } = null!;
    public DbSet<WbsTemplateItem> WbsTemplateItems { get; set; } = null!;
    public DbSet<ApproverAssignment> ApproverAssignments { get; set; } = null!;
    public DbSet<Opportunity> Opportunities { get; set; } = null!;
    public DbSet<OpportunityMember> OpportunityMembers { get; set; } = null!;
    public DbSet<QuoteVersion> QuoteVersions { get; set; } = null!;
    public DbSet<QuoteWbsItem> QuoteWbsItems { get; set; } = null!;
    public DbSet<WbsRoleAllocation> WbsRoleAllocations { get; set; } = null!;
    public DbSet<QuoteDirectExpense> QuoteDirectExpenses { get; set; } = null!;
    public DbSet<QuotePaymentMilestone> QuotePaymentMilestones { get; set; } = null!;
    public DbSet<QuoteClause> QuoteClauses { get; set; } = null!;
    public DbSet<QuoteSlaLevel> QuoteSlaLevels { get; set; } = null!;
    public DbSet<ApprovalStep> ApprovalSteps { get; set; } = null!;
    public DbSet<ApprovalLog> ApprovalLogs { get; set; } = null!;
    public DbSet<QuoteAttachment> QuoteAttachments { get; set; } = null!;
    public DbSet<ChangeAssessment> ChangeAssessments { get; set; } = null!;
    public DbSet<VarianceReview> VarianceReviews { get; set; } = null!;
    public DbSet<VarianceItem> VarianceItems { get; set; } = null!;

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

        modelBuilder.HasDefaultSchema("qtn");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(QTNDbContext).Assembly);

        modelBuilder.Entity<OutboxMessage>(b => b.ToTable("OutboxMessages", "qtn"));

        // 客戶聯絡人 Email／電話等 [Encrypted] 欄位落地加密
        modelBuilder.ApplyEncryptedProperties();
    }
}
