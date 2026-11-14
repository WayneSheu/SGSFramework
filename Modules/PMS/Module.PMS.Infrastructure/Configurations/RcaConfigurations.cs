using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Infrastructure.Configurations;

public sealed class RcaReportConfiguration : IEntityTypeConfiguration<RcaReport>
{
    public void Configure(EntityTypeBuilder<RcaReport> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("RcaReports", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Trigger).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.Summary).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.TimelineJson).IsRequired();
        builder.Property(x => x.RootCause).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.ShortTermAction).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.LongTermPrevention).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.SentNote).HasMaxLength(500);
        builder.Property(x => x.SentTo).HasMaxLength(1000);
        builder.Property(x => x.SentPdfFileName).HasMaxLength(260);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => x.TicketId).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.Status, x.DueAt });

        builder.HasOne(x => x.Ticket).WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
    }
}
