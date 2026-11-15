using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Infrastructure.Configurations;

public sealed class ContractCoopItemConfiguration : IEntityTypeConfiguration<ContractCoopItem>
{
    public void Configure(EntityTypeBuilder<ContractCoopItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ContractCoopItems", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Kind).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.ContractId, x.IsDeleted, x.Status, x.DueDate });

        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ContractExtensionNoticeConfiguration : IEntityTypeConfiguration<ContractExtensionNotice>
{
    public void Configure(EntityTypeBuilder<ContractExtensionNotice> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ContractExtensionNotices", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Mode).HasConversion<byte>();
        builder.Property(x => x.SentTo).HasMaxLength(1000);
        builder.Property(x => x.Subject).HasMaxLength(200);
        builder.Property(x => x.BodyText).HasMaxLength(4000);
        builder.Property(x => x.Closing).HasMaxLength(100);
        builder.Property(x => x.Signature).HasMaxLength(200);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.PdfFileName).HasMaxLength(260);
        builder.Property(x => x.SentByName).HasMaxLength(256);

        builder.HasIndex(x => new { x.ContractId, x.Number }).IsUnique();
        builder.HasIndex(x => x.CoopItemId);

        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
        // 協力事項不實體刪除（軟刪除），通知書是證據；避免串聯刪除
        builder.HasOne(x => x.CoopItem).WithMany(i => i.Notices).HasForeignKey(x => x.CoopItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ContractReviewRoundConfiguration : IEntityTypeConfiguration<ContractReviewRound>
{
    public void Configure(EntityTypeBuilder<ContractReviewRound> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ContractReviewRounds", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Outcome).HasConversion<byte>();
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.DeliverableItemId, x.RoundNo }).IsUnique();
        builder.HasIndex(x => new { x.ContractId, x.Outcome, x.ResponseDue });

        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.DeliverableItem).WithMany().HasForeignKey(x => x.DeliverableItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ContractAcceptanceConfiguration : IEntityTypeConfiguration<ContractAcceptance>
{
    public void Configure(EntityTypeBuilder<ContractAcceptance> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ContractAcceptances", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.DefectNote).HasMaxLength(2000);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => x.ContractId).IsUnique();
        builder.HasIndex(x => new { x.Status, x.DeemedDue });

        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ContractAmendmentConfiguration : IEntityTypeConfiguration<ContractAmendment>
{
    public void Configure(EntityTypeBuilder<ContractAmendment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ContractAmendments", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Source).HasConversion<byte>();
        builder.Property(x => x.CrNo).HasMaxLength(50);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.AmountDelta).HasPrecision(18, 2);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        // 同一張 CR 只掛帳一次（冪等）
        builder.HasIndex(x => x.ChangeRequestId).IsUnique().HasFilter("[ChangeRequestId] IS NOT NULL");
        builder.HasIndex(x => new { x.ContractId, x.EffectiveDate });

        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ContractPaymentTermConfiguration : IEntityTypeConfiguration<ContractPaymentTerm>
{
    public void Configure(EntityTypeBuilder<ContractPaymentTerm> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ContractPaymentTerms", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Percent).HasPrecision(5, 2);
        builder.Property(x => x.Trigger).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.InvoiceNo).HasMaxLength(50);
        builder.Property(x => x.InvoicedAmount).HasPrecision(18, 2);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.ContractId, x.SortOrder });
        builder.HasIndex(x => new { x.Status, x.PlannedDate });

        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
    }
}
