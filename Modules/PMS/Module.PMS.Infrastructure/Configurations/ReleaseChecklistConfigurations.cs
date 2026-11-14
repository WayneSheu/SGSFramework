using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Infrastructure.Configurations;

public sealed class ReleaseChecklistConfiguration : IEntityTypeConfiguration<ReleaseChecklist>
{
    public void Configure(EntityTypeBuilder<ReleaseChecklist> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ReleaseChecklists", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.ExceptionNote).HasMaxLength(1000);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.ProjectId, x.Version }).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.Status });

        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Items).WithOne(x => x.Checklist).HasForeignKey(x => x.ChecklistId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Signoffs).WithOne(x => x.Checklist).HasForeignKey(x => x.ChecklistId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ReleaseChecklistItemConfiguration : IEntityTypeConfiguration<ReleaseChecklistItem>
{
    public void Configure(EntityTypeBuilder<ReleaseChecklistItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ReleaseChecklistItems", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(10).IsUnicode(false).IsRequired();
        builder.Property(x => x.Severity).HasConversion<byte>();
        builder.Property(x => x.Result).HasConversion<byte>();
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.CheckedByName).HasMaxLength(256);
        builder.HasIndex(x => new { x.ChecklistId, x.Code }).IsUnique();
    }
}

public sealed class ReleaseSignoffConfiguration : IEntityTypeConfiguration<ReleaseSignoff>
{
    public void Configure(EntityTypeBuilder<ReleaseSignoff> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ReleaseSignoffs", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Role).HasConversion<byte>();
        builder.Property(x => x.SignerName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ExceptionNote).HasMaxLength(1000);
        builder.HasIndex(x => new { x.ChecklistId, x.Role }).IsUnique();
    }
}
