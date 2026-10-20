#nullable enable

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using System;

    /// <summary>
    /// 區域實驗室主管（以 TenantLabId 對應 ORG 區域實驗室，與 UserLabMapping 相同的鍵值維度）
    /// </summary>
    public class LabManager
    {
        public Guid TenantLabId { get; set; }

        public Guid ManagerUserId { get; set; }

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        public string? UpdatedByName { get; set; }
    }

    public class LabManagerConfiguration : IEntityTypeConfiguration<LabManager>
    {
        public void Configure(EntityTypeBuilder<LabManager> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("LabManagers", "core");
            builder.HasKey(x => x.TenantLabId);
            builder.HasIndex(x => x.ManagerUserId);
            builder.Property(x => x.UpdatedByName).HasMaxLength(256);
        }
    }
}
