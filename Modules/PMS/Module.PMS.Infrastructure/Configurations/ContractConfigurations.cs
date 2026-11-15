using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Infrastructure.Configurations;

public sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Contracts", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ContractNo).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CustomerContactName).HasMaxLength(100);
        builder.Property(x => x.CustomerContactEmail).HasMaxLength(256);
        builder.Property(x => x.Type).HasConversion<byte>();
        builder.Property(x => x.Industry).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.IpRule).HasConversion<byte>();
        builder.Property(x => x.ScanRule).HasConversion<byte>();
        builder.Property(x => x.LiabilityCap).HasConversion<byte>();
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.Property(x => x.LiabilityCapAmount).HasPrecision(18, 2);
        builder.Property(x => x.CoopNoticeWorkdays).HasDefaultValue(3);
        builder.Property(x => x.SupportAdvanceWorkdays).HasDefaultValue(5);
        builder.Property(x => x.SupportMaxWindowDays).HasDefaultValue(7);
        builder.Property(x => x.SupportRequiresApproval).HasDefaultValue(true);
        builder.Property(x => x.SupportSettingsReason).HasMaxLength(500);
        builder.Property(x => x.SupportSettingsUpdatedBy).HasMaxLength(256);
        builder.Property(x => x.OvertimeWeekdayRate).HasPrecision(4, 2).HasDefaultValue(1.5m);
        builder.Property(x => x.OvertimeNightRestRate).HasPrecision(4, 2).HasDefaultValue(2.0m);
        builder.Property(x => x.OvertimeHolidayRate).HasPrecision(4, 2).HasDefaultValue(2.5m);
        builder.Property(x => x.ServiceHours).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SupportChannel).HasMaxLength(200);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.SnapshotHash).HasMaxLength(64);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => x.ContractNo).IsUnique();
        builder.HasIndex(x => new { x.Status, x.DeliveryDate });
        builder.HasIndex(x => x.CustomerName);
    }
}

public sealed class ContractMemberConfiguration : IEntityTypeConfiguration<ContractMember>
{
    public void Configure(EntityTypeBuilder<ContractMember> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ContractMembers", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Role).HasConversion<byte>();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.ContractId, x.UserId, x.Role }).IsUnique();
        builder.HasIndex(x => x.UserId);

        builder.HasOne(x => x.Contract).WithMany(c => c.Members).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ContractReviewItemConfiguration : IEntityTypeConfiguration<ContractReviewItem>
{
    public void Configure(EntityTypeBuilder<ContractReviewItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ContractReviewItems", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ClientClause).HasMaxLength(1500);
        builder.Property(x => x.Note).HasMaxLength(1500);
        builder.Property(x => x.Stance).HasConversion<byte>();
        builder.Property(x => x.Result).HasConversion<byte>();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.ContractId, x.Code }).IsUnique();

        builder.HasOne(x => x.Contract).WithMany(c => c.ReviewItems).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ContractApprovalConfiguration : IEntityTypeConfiguration<ContractApproval>
{
    public void Configure(EntityTypeBuilder<ContractApproval> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ContractApprovals", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Step).HasConversion<byte>();
        builder.Property(x => x.Decision).HasConversion<byte>();
        builder.Property(x => x.Comment).HasMaxLength(1000);
        builder.Property(x => x.DecidedByName).HasMaxLength(256);

        builder.HasIndex(x => new { x.ContractId, x.Cycle, x.Step });

        builder.HasOne(x => x.Contract).WithMany(c => c.Approvals).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ContractDeliverableItemConfiguration : IEntityTypeConfiguration<ContractDeliverableItem>
{
    public void Configure(EntityTypeBuilder<ContractDeliverableItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ContractDeliverableItems", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Stage).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Format).HasMaxLength(100).IsRequired();
        builder.Property(x => x.AcceptMethod).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DeliveredChannel).HasMaxLength(50);
        builder.Property(x => x.DeliveredNote).HasMaxLength(200);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.ContractId, x.SortOrder });

        builder.HasOne(x => x.Contract).WithMany(c => c.DeliverableItems).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ContractSlaLevelConfiguration : IEntityTypeConfiguration<ContractSlaLevel>
{
    public void Configure(EntityTypeBuilder<ContractSlaLevel> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ContractSlaLevels", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Definition).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ResponseHours).HasPrecision(6, 2);
        builder.Property(x => x.DueHours).HasPrecision(6, 2);
        builder.Property(x => x.Target).HasMaxLength(500).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.ContractId, x.Level }).IsUnique();

        builder.HasOne(x => x.Contract).WithMany(c => c.SlaLevels).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ContractProjectConfiguration : IEntityTypeConfiguration<ContractProject>
{
    public void Configure(EntityTypeBuilder<ContractProject> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ContractProjects", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        // 一個專案至多屬於一份合約
        builder.HasIndex(x => x.ProjectId).IsUnique();
        builder.HasIndex(x => x.ContractId);

        builder.HasOne(x => x.Contract).WithMany(c => c.Projects).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ContractEventConfiguration : IEntityTypeConfiguration<ContractEvent>
{
    public void Configure(EntityTypeBuilder<ContractEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ContractEvents", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<byte>();
        builder.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ActorName).HasMaxLength(256);

        builder.HasIndex(x => new { x.ContractId, x.At });

        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ContractAttachmentConfiguration : IEntityTypeConfiguration<ContractAttachment>
{
    public void Configure(EntityTypeBuilder<ContractAttachment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ContractAttachments", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100);
        builder.Property(x => x.Sha256).HasMaxLength(64);
        builder.Property(x => x.Kind).HasConversion<byte>();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.ContractId, x.IsDeleted });

        builder.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Cascade);
    }
}
