using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.ComponentModel;

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    /// <summary>
    /// 定期權限覆核活動 (Access Certification Campaign)。
    /// 建立時快照當下所有（或指定角色的）成員指派，由覆核者逐一確認「保留」或「回收」。
    /// </summary>
    [DisplayName("定期權限覆核活動 (Access Certification Campaign)")]
    [Comment("定期權限覆核活動 (Access Certification Campaign)。建立時快照當下所有（或指定角色的）成員指派，由覆核者逐一確認「保留」或「回收」。")]
    public class AccessReviewCampaign
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵")]   
        public Guid Id { get; set; } = Guid.NewGuid();

        [DisplayName("覆核活動名稱")]
        [Comment("覆核活動名稱")]
        public string Name { get; set; } = string.Empty;

        [DisplayName("覆核活動描述")]
        [Comment("覆核活動描述")]
        public string? Description { get; set; }

        /// <summary>覆核期限</summary>
        [DisplayName("覆核期限")]
        [Comment("覆核期限")]
        public DateTimeOffset DueDate { get; set; }

        [DisplayName("活動狀態")]
        [Comment("活動狀態")]
        public AccessReviewCampaignStatus Status { get; set; } = AccessReviewCampaignStatus.Open;

        /// <summary>覆核範圍的角色 ID（JSON 陣列）；空陣列表示全部角色</summary>
        [DisplayName("覆核範圍的角色 ID")]
        [Comment("覆核範圍的角色 ID（JSON 陣列）；空陣列表示全部角色")]
        public string RoleScope { get; set; } = "[]";

        [DisplayName("覆核範圍的實驗室 ID")]
        [Comment("覆核範圍的實驗室 ID")]
        public string CreatedBy { get; set; } = string.Empty;

        [DisplayName("建立時間")]
        [Comment("建立時間")]   
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [DisplayName("覆核結束時間")]
        [Comment("覆核結束時間")] 
        public DateTimeOffset? ClosedAt { get; set; }

        [DisplayName("覆核結束人員")]
        [Comment("覆核結束人員")] 
        public string? ClosedBy { get; set; }
    }

    /// <summary>
    /// 定期權限覆核活動狀態
    /// </summary>
    public enum AccessReviewCampaignStatus
    {
        /// <summary>覆核中</summary>  
        Open = 0,
        /// <summary>覆核結束</summary>  
        Closed = 1
    }

    /// <summary>
    /// 覆核項目：一位使用者在一個角色上的指派
    /// </summary>
    [DisplayName("覆核項目：一位使用者在一個角色上的指派")]
    [Comment("覆核項目：一位使用者在一個角色上的指派")]
    public class AccessReviewItem
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵")]   
        public Guid Id { get; set; } = Guid.NewGuid();

        [DisplayName("覆核活動識別")]
        [Comment("覆核活動識別")] 
        public Guid CampaignId { get; set; }

        [DisplayName("覆核活動名稱")]
        [Comment("覆核活動名稱")] 
        public Guid UserId { get; set; }

        [DisplayName("使用者帳號")]
        [Comment("使用者帳號")]  
        public string UserName { get; set; } = string.Empty;

        [DisplayName("使用者全名")]
        [Comment("使用者全名")]  
        public string? FullName { get; set; }

        [DisplayName("使用者電子郵件")]
        [Comment("使用者電子郵件")]
        public string? Email { get; set; }

        [DisplayName("角色識別")]
        [Comment("角色識別")]
        public Guid RoleId { get; set; }

        [DisplayName("角色名稱")]
        [Comment("角色名稱")]   
        public string RoleName { get; set; } = string.Empty;

        /// <summary>快照：使用者主要實驗室（覆核者依此範圍覆核所屬人員）</summary>
        [DisplayName("快照：使用者主要實驗室（覆核者依此範圍覆核所屬人員）")]
        [Comment("快照：使用者主要實驗室（覆核者依此範圍覆核所屬人員）")]
        public Guid? PrimaryLabId { get; set; }

        [DisplayName("快照：使用者主要實驗室名稱（覆核者依此範圍覆核所屬人員）")]
        [Comment("快照：使用者主要實驗室名稱（覆核者依此範圍覆核所屬人員）")]
        public string? PrimaryLabName { get; set; }

        [DisplayName("覆核決定")]
        [Comment("覆核決定")]   
        public AccessReviewDecision Decision { get; set; } = AccessReviewDecision.Pending;

        [DisplayName("覆核決定說明")]
        [Comment("覆核決定說明")] 
        public string? Comment { get; set; }

        [DisplayName("覆核決定時間")]
        [Comment("覆核決定時間")]
        public Guid? DecidedBy { get; set; }

        [DisplayName("覆核決定人員名稱")]
        [Comment("覆核決定人員名稱")]   
        public string? DecidedByName { get; set; }

        [DisplayName("覆核決定時間")]
        [Comment("覆核決定時間")] 
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
