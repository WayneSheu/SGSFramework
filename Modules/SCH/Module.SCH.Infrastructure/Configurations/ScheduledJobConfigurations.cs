using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGS.Modules.SCH.Infrastructure.Entities;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Infrastructure.Persistence.Extensions;

namespace SGS.Modules.SCH.Infrastructure.Configurations;

public sealed class ScheduledJobSettingConfiguration : IEntityTypeConfiguration<ScheduledJobSetting>
{
    public void Configure(EntityTypeBuilder<ScheduledJobSetting> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ScheduledJobSettings", "sch");
        builder.HasKey(x => x.JobId);
        builder.Property(x => x.JobId).HasMaxLength(100);
        builder.Property(x => x.PauseReason).HasMaxLength(500);
        builder.Property(x => x.Cron).HasMaxLength(100);
        builder.Property(x => x.TimeZone).HasMaxLength(100);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
    }
}

public sealed class ScheduledJobRunConfiguration : IEntityTypeConfiguration<ScheduledJobRun>
{
    public void Configure(EntityTypeBuilder<ScheduledJobRun> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // 索引名稱沿用原 core 表（以 ALTER SCHEMA TRANSFER 移入 sch 後名稱不變）
        builder.ToTable("ScheduledJobRuns", "sch");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.JobId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.JobName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.HangfireJobId).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Trigger).HasMaxLength(20).IsRequired();
        builder.Property(x => x.TriggeredBy).HasMaxLength(256);
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(2000);
        builder.Property(x => x.ServerName).HasMaxLength(200);
        builder.HasIndex(x => new { x.JobId, x.StartedAt });
        builder.HasIndex(x => x.StartedAt);
        builder.HasIndex(x => new { x.HangfireJobId, x.Attempt }).IsUnique();
    }
}

public sealed class SchOutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ConfigureOutboxTable("sch");
    }
}
