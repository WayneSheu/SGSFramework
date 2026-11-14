using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Infrastructure.Configurations;

public sealed class ChangeRequestSequenceConfiguration : IEntityTypeConfiguration<ChangeRequestSequence>
{
    public void Configure(EntityTypeBuilder<ChangeRequestSequence> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ChangeRequestSequences", "pms");
        builder.HasKey(x => x.ProjectId);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
    }
}

public sealed class ChangeRequestConfiguration : IEntityTypeConfiguration<ChangeRequest>
{
    public void Configure(EntityTypeBuilder<ChangeRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ChangeRequests", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CrNo).HasMaxLength(40).IsUnicode(false).IsRequired();
        builder.Property(x => x.Kind).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.Decision).HasConversion<byte?>();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.DecisionRationale).HasMaxLength(2000);
        builder.Property(x => x.SwappedOutItems).HasMaxLength(2000);
        builder.Property(x => x.QuoteAmount).HasPrecision(18, 2);
        builder.Property(x => x.ContentHash).HasMaxLength(64).IsUnicode(false).IsFixedLength();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => x.CrNo).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.Status, x.EvalDueAt });
        builder.HasIndex(x => x.RequesterUserId);
        builder.HasIndex(x => x.SourceTicketId);

        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Assessment).WithOne(x => x.ChangeRequest).HasForeignKey<CrImpactAssessment>(x => x.ChangeRequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Signoffs).WithOne(x => x.ChangeRequest).HasForeignKey(x => x.ChangeRequestId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Events).WithOne(x => x.ChangeRequest).HasForeignKey(x => x.ChangeRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CrImpactAssessmentConfiguration : IEntityTypeConfiguration<CrImpactAssessment>
{
    public void Configure(EntityTypeBuilder<CrImpactAssessment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CrImpactAssessments", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AffectedLayers).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ApiImpact).HasConversion<byte>();
        builder.Property(x => x.CouplingImpact).HasConversion<byte>();
        builder.Property(x => x.HoursBe).HasPrecision(8, 2);
        builder.Property(x => x.HoursFe).HasPrecision(8, 2);
        builder.Property(x => x.HoursVerify).HasPrecision(8, 2);
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(x => x.ChangeRequestId).IsUnique();
    }
}

public sealed class CrSignoffConfiguration : IEntityTypeConfiguration<CrSignoff>
{
    public void Configure(EntityTypeBuilder<CrSignoff> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CrSignoffs", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Role).HasConversion<byte>();
        builder.Property(x => x.SignerName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ContentHash).HasMaxLength(64).IsUnicode(false).IsFixedLength().IsRequired();
        builder.Property(x => x.Comment).HasMaxLength(1000);
        // 每個角色每張 CR 只能簽一次
        builder.HasIndex(x => new { x.ChangeRequestId, x.Role }).IsUnique();
    }
}

public sealed class CrEventConfiguration : IEntityTypeConfiguration<CrEvent>
{
    public void Configure(EntityTypeBuilder<CrEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CrEvents", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasConversion<byte>();
        builder.Property(x => x.Detail).HasMaxLength(1000);
        builder.Property(x => x.ActorName).HasMaxLength(256);
        builder.HasIndex(x => new { x.ChangeRequestId, x.At });
    }
}
