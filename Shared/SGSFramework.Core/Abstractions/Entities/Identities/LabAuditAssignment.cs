#nullable enable

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using System;

    /// <summary>
    /// 稽核指派：指定使用者於起訖期間內對某區域實驗室執行稽核職責。
    /// 有效期間內，使用者所持有的稽核角色（<see cref="ApplicationRole.RequiresAuditAssignment"/>）權限於該實驗室生效，
    /// 且該實驗室會出現在使用者可切換的實驗室清單中；期間外或撤銷後自動失效。紀錄不刪除，保留完整稽核軌跡。
    /// </summary>
    public class LabAuditAssignment
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }

        /// <summary>受稽核的區域實驗室（ORG Organization.TenantLabId）</summary>
        public Guid TenantLabId { get; set; }

        /// <summary>生效起始（含）</summary>
        public DateTimeOffset EffectiveFrom { get; set; }

        /// <summary>生效迄止（含）</summary>
        public DateTimeOffset EffectiveTo { get; set; }

        /// <summary>稽核原因 / 稽核案號</summary>
        public string Reason { get; set; } = string.Empty;

        public Guid? AssignedBy { get; set; }

        public string? AssignedByName { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset? RevokedAt { get; set; }

        public string? RevokedByName { get; set; }

        public string? RevokeReason { get; set; }

        /// <summary>指定時間點是否有效（未撤銷且於起訖期間內）</summary>
        public bool IsActiveAt(DateTimeOffset now) => RevokedAt is null && EffectiveFrom <= now && now <= EffectiveTo;
    }

    public class LabAuditAssignmentConfiguration : IEntityTypeConfiguration<LabAuditAssignment>
    {
        public void Configure(EntityTypeBuilder<LabAuditAssignment> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("LabAuditAssignments", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            builder.Property(x => x.AssignedByName).HasMaxLength(256);
            builder.Property(x => x.RevokedByName).HasMaxLength(256);
            builder.Property(x => x.RevokeReason).HasMaxLength(500);
            builder.HasIndex(x => new { x.UserId, x.TenantLabId });
            builder.HasIndex(x => x.TenantLabId);
        }
    }
}
