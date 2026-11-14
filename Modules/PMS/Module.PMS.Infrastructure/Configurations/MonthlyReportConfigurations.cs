using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Infrastructure.Configurations;

public sealed class MonthlyReportConfiguration : IEntityTypeConfiguration<MonthlyReport>
{
    public void Configure(EntityTypeBuilder<MonthlyReport> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("MonthlyReports", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Period).HasMaxLength(7).IsUnicode(false).IsFixedLength().IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false).IsFixedLength();
        builder.Property(x => x.SlaOnTimeRate).HasPrecision(5, 4);
        builder.Property(x => x.MttrHours).HasPrecision(10, 2);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.ProjectId, x.Period }).IsUnique();

        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
