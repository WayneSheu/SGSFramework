using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGS.Modules.CAL.Infrastructure.Entities;

namespace SGS.Modules.CAL.Infrastructure.Configurations;

public sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Holidays", "cal");
        builder.HasKey(x => x.HolidayDate);
        builder.Property(x => x.HolidayDate).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Source).HasConversion<byte>().HasDefaultValue(HolidaySource.Manual);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
    }
}

public sealed class HolidaySyncRunConfiguration : IEntityTypeConfiguration<HolidaySyncRun>
{
    public void Configure(EntityTypeBuilder<HolidaySyncRun> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("HolidaySyncRuns", "cal");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Mode).HasConversion<byte>();
        builder.Property(x => x.Actor).HasMaxLength(256);
        builder.Property(x => x.SourceUrl).HasMaxLength(500);
        builder.Property(x => x.Message).HasMaxLength(1000);
        builder.HasIndex(x => x.RunAtUtc);
    }
}

public sealed class CalendarEventAttendeeConfiguration : IEntityTypeConfiguration<CalendarEventAttendee>
{
    public void Configure(EntityTypeBuilder<CalendarEventAttendee> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CalendarEventAttendees", "cal");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.EventId, x.UserId }).IsUnique();
        builder.HasOne(x => x.Event).WithMany(e => e.Attendees).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CalendarEventReminderConfiguration : IEntityTypeConfiguration<CalendarEventReminder>
{
    public void Configure(EntityTypeBuilder<CalendarEventReminder> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CalendarEventReminders", "cal");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.EventId, x.MinutesBefore }).IsUnique();
        builder.HasIndex(x => x.SentAtUtc);
        builder.HasOne(x => x.Event).WithMany(e => e.Reminders).HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class CalendarConfiguration : IEntityTypeConfiguration<Calendar>
{
    public void Configure(EntityTypeBuilder<Calendar> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Calendars", "cal");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Type).HasConversion<byte>();
        builder.Property(x => x.Color).HasMaxLength(7).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => new { x.Type, x.IsArchived });
    }
}

public sealed class CalendarEventConfiguration : IEntityTypeConfiguration<CalendarEvent>
{
    public void Configure(EntityTypeBuilder<CalendarEvent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CalendarEvents", "cal");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Kind).HasConversion<byte>();
        builder.Property(x => x.Location).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Category).HasMaxLength(50);
        builder.Property(x => x.ExternalSource).HasMaxLength(50);
        builder.Property(x => x.ExternalKey).HasMaxLength(100);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => new { x.CalendarId, x.StartAt });
        builder.HasIndex(x => new { x.ExternalSource, x.ExternalKey }).IsUnique().HasFilter("[ExternalKey] IS NOT NULL");

        builder.HasOne(x => x.Calendar).WithMany(c => c.Events).HasForeignKey(x => x.CalendarId).OnDelete(DeleteBehavior.Cascade);
    }
}
