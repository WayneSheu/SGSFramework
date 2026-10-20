using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    /// <summary>
    /// 定期權限覆核活動 (Access Certification Campaign)。
    /// 建立時快照當下所有（或指定角色的）成員指派，由覆核者逐一確認「保留」或「回收」。
    /// </summary>
    public class AccessReviewCampaign
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>覆核期限</summary>
        public DateTimeOffset DueDate { get; set; }

        public AccessReviewCampaignStatus Status { get; set; } = AccessReviewCampaignStatus.Open;

        /// <summary>覆核範圍的角色 ID（JSON 陣列）；空陣列表示全部角色</summary>
        public string RoleScope { get; set; } = "[]";

        public string CreatedBy { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? ClosedAt { get; set; }

        public string? ClosedBy { get; set; }
    }

    public enum AccessReviewCampaignStatus
    {
        Open = 0,
        Closed = 1
    }

    /// <summary>
    /// 覆核項目：一位使用者在一個角色上的指派
    /// </summary>
    public class AccessReviewItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid CampaignId { get; set; }

        public Guid UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string? FullName { get; set; }

        public Guid RoleId { get; set; }

        public string RoleName { get; set; } = string.Empty;

        /// <summary>快照：使用者主要實驗室（覆核者依此範圍覆核所屬人員）</summary>
        public Guid? PrimaryLabId { get; set; }

        public string? PrimaryLabName { get; set; }

        public AccessReviewDecision Decision { get; set; } = AccessReviewDecision.Pending;

        public string? Comment { get; set; }

        public Guid? DecidedBy { get; set; }

        public string? DecidedByName { get; set; }

        public DateTimeOffset? DecidedAt { get; set; }
    }

    public enum AccessReviewDecision
    {
        Pending = 0,
        Keep = 1,
        Revoke = 2
    }

    public class AccessReviewCampaignConfiguration : IEntityTypeConfiguration<AccessReviewCampaign>
    {
        public void Configure(EntityTypeBuilder<AccessReviewCampaign> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("AccessReviewCampaigns", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(1000);
            builder.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired();
            builder.Property(x => x.ClosedBy).HasMaxLength(256);
            builder.HasIndex(x => new { x.Status, x.CreatedAt });
        }
    }

    public class AccessReviewItemConfiguration : IEntityTypeConfiguration<AccessReviewItem>
    {
        public void Configure(EntityTypeBuilder<AccessReviewItem> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("AccessReviewItems", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.UserName).HasMaxLength(256).IsRequired();
            builder.Property(x => x.FullName).HasMaxLength(256);
            builder.Property(x => x.RoleName).HasMaxLength(256).IsRequired();
            builder.Property(x => x.PrimaryLabName).HasMaxLength(256);
            builder.Property(x => x.Comment).HasMaxLength(1000);
            builder.Property(x => x.DecidedByName).HasMaxLength(256);
            builder.HasIndex(x => new { x.CampaignId, x.Decision });
            builder.HasIndex(x => new { x.CampaignId, x.PrimaryLabId });
        }
    }
}
