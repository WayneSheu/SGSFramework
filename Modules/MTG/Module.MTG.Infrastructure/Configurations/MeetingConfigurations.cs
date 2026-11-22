using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGS.Modules.MTG.Infrastructure.Entities;
using SGSFramework.Core.Abstractions.Outbox;
using SGSFramework.Infrastructure.Persistence.Extensions;

namespace SGS.Modules.MTG.Infrastructure.Configurations;

public sealed class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Meetings", "mtg");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ScenarioCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.StatusMessage).HasMaxLength(500);
        builder.Property(x => x.Organizer).HasMaxLength(256).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(x => x.MeetingTimeUtc);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ProjectId);
        builder.HasMany(x => x.Segments).WithOne(x => x.Meeting).HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.MinutesVersions).WithOne(x => x.Meeting).HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Audios).WithOne(x => x.Meeting).HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MeetingAudioConfiguration : IEntityTypeConfiguration<MeetingAudio>
{
    public void Configure(EntityTypeBuilder<MeetingAudio> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("MeetingAudios", "mtg");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.StatusMessage).HasMaxLength(500);
        builder.Property(x => x.Language).HasMaxLength(20);
        builder.Property(x => x.SummarizeScenarioCode).HasMaxLength(50);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(x => x.MeetingId);
        builder.HasIndex(x => x.FileId).IsUnique();
        builder.HasIndex(x => x.Status);
    }
}

public sealed class TranscriptSegmentConfiguration : IEntityTypeConfiguration<TranscriptSegment>
{
    public void Configure(EntityTypeBuilder<TranscriptSegment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("TranscriptSegments", "mtg");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Speaker).HasMaxLength(50);
        builder.Property(x => x.Text).IsRequired();
        builder.Property(x => x.ClientSegmentId).HasMaxLength(64);
        builder.HasIndex(x => new { x.MeetingId, x.Seq }).IsUnique();
        builder.HasIndex(x => x.AudioId); // 重新轉錄時依來源音檔取代片段
        // 冪等去重：同一會議內相同的用戶端片段識別只能有一筆（可空值不受限）
        builder.HasIndex(x => new { x.MeetingId, x.ClientSegmentId }).IsUnique().HasFilter("[ClientSegmentId] IS NOT NULL");
    }
}

public sealed class MeetingMinutesConfiguration : IEntityTypeConfiguration<MeetingMinutes>
{
    public void Configure(EntityTypeBuilder<MeetingMinutes> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("MeetingMinutes", "mtg");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ScenarioCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Markdown).IsRequired();
        builder.Property(x => x.Model).HasMaxLength(100).IsRequired();
        builder.Property(x => x.GeneratedBy).HasMaxLength(256).IsRequired();
        builder.HasIndex(x => new { x.MeetingId, x.Version }).IsUnique();
    }
}

public sealed class PromptTemplateConfiguration : IEntityTypeConfiguration<PromptTemplate>
{
    public void Configure(EntityTypeBuilder<PromptTemplate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("PromptTemplates", "mtg");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ScenarioCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.MapPrompt).IsRequired();
        builder.Property(x => x.ReducePrompt).IsRequired();
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(x => x.ScenarioCode).IsUnique();
    }
}

public sealed class GlossaryEntryConfiguration : IEntityTypeConfiguration<GlossaryEntry>
{
    public void Configure(EntityTypeBuilder<GlossaryEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("GlossaryEntries", "mtg");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Term).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Variants).HasMaxLength(1000);
        builder.Property(x => x.Category).HasMaxLength(50);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(x => x.Term).IsUnique();
    }
}

public sealed class MtgOutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ConfigureOutboxTable("mtg");
    }
}
