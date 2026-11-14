using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGS.Modules.PMS.Infrastructure.Entities;

namespace SGS.Modules.PMS.Infrastructure.Configurations;

public sealed class TicketSequenceConfiguration : IEntityTypeConfiguration<TicketSequence>
{
    public void Configure(EntityTypeBuilder<TicketSequence> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TicketSequences", "pms");
        builder.HasKey(x => x.ProjectId);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
    }
}

public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(x => x.Id);
        builder.Property(x => x.TicketNo).HasMaxLength(30).IsUnicode(false).IsRequired();
        builder.Property(x => x.Type).HasConversion<byte>();
        builder.Property(x => x.Priority).HasConversion<byte?>();
        builder.Property(x => x.Stage).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.Source).HasConversion<byte>();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.Environment).HasMaxLength(50);
        builder.Property(x => x.Impact).HasMaxLength(100);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => x.TicketNo).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.Status, x.DueAt });
        builder.HasIndex(x => x.FixerUserId);
        builder.HasIndex(x => x.VerifierUserId);
        builder.HasIndex(x => x.ReporterUserId);

        // SoD 資料層防線：修復人與驗證人不可為同一人（服務層另有檢查）
        builder.ToTable("Tickets", "pms", t =>
            t.HasCheckConstraint("CK_Tickets_FixerNotVerifier", "[FixerUserId] IS NULL OR [VerifierUserId] IS NULL OR [FixerUserId] <> [VerifierUserId]"));

        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Events).WithOne(x => x.Ticket).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Attachments).WithOne(x => x.Ticket).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TicketEventConfiguration : IEntityTypeConfiguration<TicketEvent>
{
    public void Configure(EntityTypeBuilder<TicketEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TicketEvents", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EventType).HasConversion<byte>();
        builder.Property(x => x.Detail).HasMaxLength(1000);
        builder.Property(x => x.ActorName).HasMaxLength(256);
        builder.HasIndex(x => new { x.TicketId, x.At });
    }
}

public sealed class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TicketAttachments", "pms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.OriginalFileName).HasMaxLength(260);
        builder.Property(x => x.ContentType).HasMaxLength(100);
        builder.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false).IsFixedLength();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(x => new { x.TicketId, x.IsDeleted });
        builder.HasIndex(x => x.FileId);
    }
}

public sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Holidays", "pms");
        builder.HasKey(x => x.HolidayDate);
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
    }
}
