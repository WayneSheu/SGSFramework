using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Infrastructure.Configurations;

public sealed class UatSignoffConfiguration : IEntityTypeConfiguration<UatSignoff>
{
    public void Configure(EntityTypeBuilder<UatSignoff> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("UatSignoffs", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Version).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(x => x.ScopeNote).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.ContentHash).HasMaxLength(64).IsUnicode(false).IsFixedLength().IsRequired();
        builder.Property(x => x.ProxyReason).HasMaxLength(1000);
        builder.Property(x => x.Comment).HasMaxLength(1000);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        // 同專案同版本只能有一張有效（未取消）的驗收單；取消後可重新建立
        builder.HasIndex(x => new { x.ProjectId, x.Version }).IsUnique().HasFilter("[Status] <> 3");
        builder.HasIndex(x => new { x.ProjectId, x.Status });

        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Attachments).WithOne(x => x.UatSignoff).HasForeignKey(x => x.UatSignoffId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UatAttachmentConfiguration : IEntityTypeConfiguration<UatAttachment>
{
    public void Configure(EntityTypeBuilder<UatAttachment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("UatAttachments", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.OriginalFileName).HasMaxLength(260);
        builder.Property(x => x.ContentType).HasMaxLength(100);
        builder.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false).IsFixedLength();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(x => new { x.UatSignoffId, x.IsDeleted });
    }
}
