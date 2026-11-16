using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGS.Modules.QTN.Infrastructure.Entities;

namespace SGS.Modules.QTN.Infrastructure.Configurations;

internal static class QtnConfig
{
    public const string Schema = "qtn";

    public static void Audit<T>(this EntityTypeBuilder<T> b) where T : class, SGSFramework.Core.Abstractions.Entities.AuditLogs.IAuditable
    {
        b.Property(x => x.CreatedBy).HasMaxLength(256);
        b.Property(x => x.UpdatedBy).HasMaxLength(256);
    }

    public static PropertyBuilder<decimal> Money(this PropertyBuilder<decimal> p) => p.HasPrecision(18, 2);

    public static PropertyBuilder<decimal?> Money(this PropertyBuilder<decimal?> p) => p.HasPrecision(18, 2);

    public static PropertyBuilder<decimal> Days(this PropertyBuilder<decimal> p) => p.HasPrecision(10, 2);

    public static PropertyBuilder<decimal?> Days(this PropertyBuilder<decimal?> p) => p.HasPrecision(10, 2);
}

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("Customers", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.TaxId).HasMaxLength(20);
        b.Property(x => x.Industry).HasMaxLength(50);
        b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.ContactName).HasMaxLength(100);
        b.Property(x => x.ContactEmail).HasMaxLength(500);
        b.Property(x => x.ContactPhone).HasMaxLength(300);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.Audit();
    }
}

public sealed class RateRoleConfiguration : IEntityTypeConfiguration<RateRole>
{
    public void Configure(EntityTypeBuilder<RateRole> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("RateRoles", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.BaseCostRate).Money();
        b.Property(x => x.BaseListRate).Money();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasMany(x => x.History).WithOne(h => h.RateRole).HasForeignKey(h => h.RateRoleId).OnDelete(DeleteBehavior.Cascade);
        b.Audit();
    }
}

public sealed class RateRoleHistoryConfiguration : IEntityTypeConfiguration<RateRoleHistory>
{
    public void Configure(EntityTypeBuilder<RateRoleHistory> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("RateRoleHistories", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.CostRate).Money();
        b.Property(x => x.ListRate).Money();
        b.Property(x => x.Reason).HasMaxLength(500);
        b.HasIndex(x => new { x.RateRoleId, x.EffectiveAt });
        b.Audit();
    }
}

public sealed class WbsTemplateConfiguration : IEntityTypeConfiguration<WbsTemplate>
{
    public void Configure(EntityTypeBuilder<WbsTemplate> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("WbsTemplates", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(30).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasMany(x => x.Items).WithOne(i => i.Template).HasForeignKey(i => i.TemplateId).OnDelete(DeleteBehavior.Cascade);
        b.Audit();
    }
}

public sealed class WbsTemplateItemConfiguration : IEntityTypeConfiguration<WbsTemplateItem>
{
    public void Configure(EntityTypeBuilder<WbsTemplateItem> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("WbsTemplateItems", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Deliverable).HasMaxLength(500);
        b.Property(x => x.Phase).HasConversion<byte>();
        b.Property(x => x.Optimistic).Days();
        b.Property(x => x.MostLikely).Days();
        b.Property(x => x.Pessimistic).Days();
        b.Audit();
    }
}

public sealed class ApproverAssignmentConfiguration : IEntityTypeConfiguration<ApproverAssignment>
{
    public void Configure(EntityTypeBuilder<ApproverAssignment> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("ApproverAssignments", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.Role).HasConversion<byte>();
        b.HasIndex(x => new { x.Role, x.UserId }).IsUnique();
        b.Audit();
    }
}

public sealed class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("Opportunities", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.OpportunityNo).HasMaxLength(20).IsRequired();
        b.HasIndex(x => x.OpportunityNo).IsUnique();
        b.Property(x => x.CrmRef).HasMaxLength(50);
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Category).HasConversion<byte>();
        b.Property(x => x.PricingModel).HasConversion<byte>();
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.Scope).HasMaxLength(4000);
        b.Property(x => x.SecurityLevel).HasMaxLength(100);
        b.Property(x => x.NonFunctional).HasMaxLength(2000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(x => x.Members).WithOne(m => m.Opportunity).HasForeignKey(m => m.OpportunityId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Quotes).WithOne(q => q.Opportunity).HasForeignKey(q => q.OpportunityId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.Status);
        b.Audit();
    }
}

public sealed class OpportunityMemberConfiguration : IEntityTypeConfiguration<OpportunityMember>
{
    public void Configure(EntityTypeBuilder<OpportunityMember> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("OpportunityMembers", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.Role).HasConversion<byte>();
        b.HasIndex(x => new { x.OpportunityId, x.UserId, x.Role }).IsUnique();
        b.HasIndex(x => x.UserId);
        b.Audit();
    }
}

public sealed class QuoteVersionConfiguration : IEntityTypeConfiguration<QuoteVersion>
{
    public void Configure(EntityTypeBuilder<QuoteVersion> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("QuoteVersions", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.QuoteNo).HasMaxLength(30).IsRequired();
        b.Property(x => x.VersionLabel).HasMaxLength(20).IsRequired();
        b.HasIndex(x => new { x.QuoteNo, x.Major, x.Minor }).IsUnique();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.PricingModel).HasConversion<byte>();
        b.Property(x => x.MarginLight).HasConversion<byte>();
        b.Property(x => x.TechReviewNote).HasMaxLength(1000);
        b.Property(x => x.TotalQuotedDays).Days();
        b.Property(x => x.LaborCost).Money();
        b.Property(x => x.Overhead).Money();
        b.Property(x => x.DirectExpense).Money();
        b.Property(x => x.TotalCost).Money();
        b.Property(x => x.LaborListTotal).Money();
        b.Property(x => x.IpLicenseFee).Money();
        b.Property(x => x.ListTotal).Money();
        b.Property(x => x.DiscountAmount).Money();
        b.Property(x => x.DiscountReason).HasMaxLength(1000);
        b.Property(x => x.FinalPrice).Money();
        b.Property(x => x.TaxRate).HasPrecision(5, 4);
        b.Property(x => x.TaxAmount).Money();
        b.Property(x => x.FinalPriceWithTax).Money();
        b.Property(x => x.MarginRate).HasPrecision(9, 6);
        b.Property(x => x.BonusFactor).HasPrecision(4, 2);
        b.Property(x => x.SpecialApprovalReason).HasMaxLength(1000);
        b.Property(x => x.RetainerMonthlyDaysCap).Days();
        b.Property(x => x.RetainerMonthlyFee).Money();
        b.Property(x => x.OverageDailyRate).Money();
        b.Property(x => x.OffHoursMultiplier).HasPrecision(4, 2);
        b.Property(x => x.ContentHash).HasMaxLength(64);
        b.Property(x => x.OutcomeNote).HasMaxLength(1000);
        b.Property(x => x.PmsContractNo).HasMaxLength(30);
        b.Property(x => x.Notes).HasMaxLength(2000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.Status);
        b.HasIndex(x => x.ValidUntil);

        b.HasMany(x => x.WbsItems).WithOne(w => w.QuoteVersion).HasForeignKey(w => w.QuoteVersionId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Expenses).WithOne().HasForeignKey(e => e.QuoteVersionId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Milestones).WithOne().HasForeignKey(e => e.QuoteVersionId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Clauses).WithOne().HasForeignKey(e => e.QuoteVersionId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.SlaLevels).WithOne().HasForeignKey(e => e.QuoteVersionId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.ApprovalSteps).WithOne(s => s.QuoteVersion).HasForeignKey(s => s.QuoteVersionId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Attachments).WithOne().HasForeignKey(a => a.QuoteVersionId).OnDelete(DeleteBehavior.Cascade);
        b.Audit();
    }
}

public sealed class QuoteWbsItemConfiguration : IEntityTypeConfiguration<QuoteWbsItem>
{
    public void Configure(EntityTypeBuilder<QuoteWbsItem> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("QuoteWbsItems", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.WbsCode).HasMaxLength(30).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Deliverable).HasMaxLength(1000);
        b.Property(x => x.Phase).HasConversion<byte>();
        b.Property(x => x.KTech).HasPrecision(4, 2);
        b.Property(x => x.KReq).HasPrecision(4, 2);
        b.HasIndex(x => new { x.QuoteVersionId, x.ParentId, x.SortOrder });
        b.HasMany(x => x.Allocations).WithOne(a => a.WbsItem).HasForeignKey(a => a.WbsItemId).OnDelete(DeleteBehavior.Cascade);
        b.Audit();
    }
}

public sealed class WbsRoleAllocationConfiguration : IEntityTypeConfiguration<WbsRoleAllocation>
{
    public void Configure(EntityTypeBuilder<WbsRoleAllocation> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("WbsRoleAllocations", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.RoleName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Optimistic).Days();
        b.Property(x => x.MostLikely).Days();
        b.Property(x => x.Pessimistic).Days();
        b.Property(x => x.ExpectedDays).Days();
        b.Property(x => x.QuotedDays).Days();
        b.Property(x => x.SnapshotCostRate).Money();
        b.Property(x => x.SnapshotListRate).Money();
        b.HasIndex(x => x.RateRoleId);
        b.Audit();
    }
}

public sealed class QuoteDirectExpenseConfiguration : IEntityTypeConfiguration<QuoteDirectExpense>
{
    public void Configure(EntityTypeBuilder<QuoteDirectExpense> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("QuoteDirectExpenses", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.Category).HasConversion<byte>();
        b.Property(x => x.Description).HasMaxLength(300).IsRequired();
        b.Property(x => x.Amount).Money();
        b.Audit();
    }
}

public sealed class QuotePaymentMilestoneConfiguration : IEntityTypeConfiguration<QuotePaymentMilestone>
{
    public void Configure(EntityTypeBuilder<QuotePaymentMilestone> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("QuotePaymentMilestones", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Percent).HasPrecision(5, 2);
        b.Property(x => x.Amount).Money();
        b.Property(x => x.AmountWithTax).Money();
        b.Property(x => x.Trigger).HasMaxLength(500);
        b.HasIndex(x => new { x.QuoteVersionId, x.Seq }).IsUnique();
        b.Audit();
    }
}

public sealed class QuoteClauseConfiguration : IEntityTypeConfiguration<QuoteClause>
{
    public void Configure(EntityTypeBuilder<QuoteClause> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("QuoteClauses", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(40).IsRequired();
        b.Property(x => x.Title).HasMaxLength(100).IsRequired();
        b.Property(x => x.Body).HasMaxLength(4000).IsRequired();
        b.Audit();
    }
}

public sealed class QuoteSlaLevelConfiguration : IEntityTypeConfiguration<QuoteSlaLevel>
{
    public void Configure(EntityTypeBuilder<QuoteSlaLevel> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("QuoteSlaLevels", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.Level).HasMaxLength(10).IsRequired();
        b.Property(x => x.Definition).HasMaxLength(500).IsRequired();
        b.Property(x => x.ResponseTime).HasMaxLength(50).IsRequired();
        b.Property(x => x.ResolveTarget).HasMaxLength(50).IsRequired();
        b.Audit();
    }
}

public sealed class ApprovalStepConfiguration : IEntityTypeConfiguration<ApprovalStep>
{
    public void Configure(EntityTypeBuilder<ApprovalStep> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("ApprovalSteps", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.Role).HasConversion<byte>();
        b.Property(x => x.State).HasConversion<byte>();
        b.Property(x => x.DecidedByName).HasMaxLength(256);
        b.Property(x => x.Comment).HasMaxLength(1000);
        b.Property(x => x.ContentHash).HasMaxLength(64);
        b.HasIndex(x => new { x.QuoteVersionId, x.Cycle, x.Seq }).IsUnique();
        b.HasIndex(x => new { x.Role, x.State });
        b.Audit();
    }
}

public sealed class ApprovalLogConfiguration : IEntityTypeConfiguration<ApprovalLog>
{
    public void Configure(EntityTypeBuilder<ApprovalLog> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("ApprovalLogs", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasConversion<byte>();
        b.Property(x => x.Role).HasConversion<byte?>();
        b.Property(x => x.ActorName).HasMaxLength(256).IsRequired();
        b.Property(x => x.IpAddress).HasMaxLength(64);
        b.Property(x => x.Message).HasMaxLength(2000);
        b.Property(x => x.ContentHash).HasMaxLength(64);
        b.HasIndex(x => new { x.QuoteVersionId, x.At });
    }
}

public sealed class QuoteAttachmentConfiguration : IEntityTypeConfiguration<QuoteAttachment>
{
    public void Configure(EntityTypeBuilder<QuoteAttachment> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("QuoteAttachments", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasConversion<byte>();
        b.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(100);
        b.Property(x => x.Sha256).HasMaxLength(64);
        b.HasIndex(x => x.FileId);
        b.Audit();
    }
}

public sealed class ChangeAssessmentConfiguration : IEntityTypeConfiguration<ChangeAssessment>
{
    public void Configure(EntityTypeBuilder<ChangeAssessment> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("ChangeAssessments", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.AssessmentNo).HasMaxLength(40).IsRequired();
        b.HasIndex(x => x.AssessmentNo).IsUnique();
        b.Property(x => x.Requester).HasConversion<byte>();
        b.Property(x => x.RequesterName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Title).HasMaxLength(200).IsRequired();
        b.Property(x => x.OriginalSpec).HasMaxLength(4000).IsRequired();
        b.Property(x => x.ChangedSpec).HasMaxLength(4000).IsRequired();
        b.Property(x => x.NewDevDays).Days();
        b.Property(x => x.ReworkDays).Days();
        b.Property(x => x.RegressionDays).Days();
        b.Property(x => x.PmDays).Days();
        b.Property(x => x.AmountUntaxed).Money();
        b.Property(x => x.AmountWithTax).Money();
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.CustomerSignatory).HasMaxLength(100);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.OpportunityId);
        b.HasIndex(x => new { x.Status, x.AssessmentDueDate });
        b.Audit();
    }
}

public sealed class VarianceReviewConfiguration : IEntityTypeConfiguration<VarianceReview>
{
    public void Configure(EntityTypeBuilder<VarianceReview> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("VarianceReviews", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.HasIndex(x => x.QuoteVersionId).IsUnique();
        b.Property(x => x.Status).HasConversion<byte>();
        b.Property(x => x.RetrospectiveNotes).HasMaxLength(4000);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasMany(x => x.Items).WithOne(i => i.Review).HasForeignKey(i => i.ReviewId).OnDelete(DeleteBehavior.Cascade);
        b.Audit();
    }
}

public sealed class VarianceItemConfiguration : IEntityTypeConfiguration<VarianceItem>
{
    public void Configure(EntityTypeBuilder<VarianceItem> b)
    {
        ArgumentNullException.ThrowIfNull(b);
        b.ToTable("VarianceItems", QtnConfig.Schema);
        b.HasKey(x => x.Id);
        b.Property(x => x.WbsCode).HasMaxLength(30).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.QuotedDays).Days();
        b.Property(x => x.ActualDays).Days();
        b.Property(x => x.VarianceRate).HasPrecision(9, 6);
        b.Property(x => x.RootCause).HasMaxLength(1000);
        b.Audit();
    }
}
