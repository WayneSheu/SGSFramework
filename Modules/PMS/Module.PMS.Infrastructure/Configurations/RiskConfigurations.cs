using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Infrastructure.Configurations;

public sealed class RiskItemConfiguration : IEntityTypeConfiguration<RiskItem>
{
    public void Configure(EntityTypeBuilder<RiskItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("RiskItems", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Level).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.Mitigation).HasMaxLength(1000);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.ProjectId, x.IsVoided, x.Status, x.Level });
        builder.HasIndex(x => x.OwnerUserId);

        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
