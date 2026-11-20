using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGS.Modules.MAN.Infrastructure.Entities;

namespace SGS.Modules.MAN.Infrastructure.Configurations;

public sealed class ManualTopicConfiguration : IEntityTypeConfiguration<ManualTopic>
{
    public void Configure(EntityTypeBuilder<ManualTopic> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ManualTopics", "man");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FeatureKey).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.FeatureKey).IsUnique();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ModuleName).HasMaxLength(100);
        builder.Property(x => x.Tooltip).HasMaxLength(200).IsRequired();
        builder.Property(x => x.StepsMarkdown).IsRequired();
        builder.Property(x => x.Precautions).IsRequired();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(ManualTopicStatus.Draft);
        builder.Property(x => x.Source).HasConversion<byte>().HasDefaultValue(ManualContentSource.Manual);
        builder.Property(x => x.PublishedBy).HasMaxLength(256);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(x => new { x.Status, x.SortOrder });
    }
}

public sealed class GenerationJobConfiguration : IEntityTypeConfiguration<GenerationJob>
{
    public void Configure(EntityTypeBuilder<GenerationJob> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("GenerationJobs", "man");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FeatureKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Status).HasConversion<byte>();
        builder.Property(x => x.Model).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(1000);
        builder.Property(x => x.CreatedBy).HasMaxLength(256);
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => x.Status);
    }
}
