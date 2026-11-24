#nullable enable

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using System;
    using System.ComponentModel;

    /// <summary>
    /// 稽核指派異動歷程：每次建立、修改、撤銷各留一筆不可變更的事件，保留完整稽核軌跡
    /// （<see cref="LabAuditAssignment"/> 本身只保留最新狀態）。
    /// </summary>
    [DisplayName("實驗室稽核指派異動歷程")]
    [Comment("實驗室稽核指派異動歷程：每次建立、修改、撤銷各留一筆不可變更的事件，保留完整稽核軌跡（LabAuditAssignment 本身只保留最新狀態）。")]
    public class LabAuditAssignmentHistory
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [DisplayName("稽核指派識別鍵")]
        [Comment("稽核指派識別鍵（LabAuditAssignment.Id）")]
        public Guid AssignmentId { get; set; }

        /// <summary>事件類型：created / updated / revoked</summary>
        [DisplayName("事件類型")]
        [Comment("事件類型：created / updated / revoked")]
        public string Action { get; set; } = string.Empty;

        [DisplayName("操作人員識別鍵")]
        [Comment("操作人員識別鍵")]
        public Guid? ActorId { get; set; }

        [DisplayName("操作人員名稱")]
        [Comment("操作人員名稱")]
        public string? ActorName { get; set; }

        [DisplayName("發生時間")]
        [Comment("發生時間")]
        public DateTimeOffset OccurredAt { get; set; }

        /// <summary>異動摘要（建立：稽核員 / 實驗室 / 期間 / 原因；修改：變更前後；撤銷：撤銷原因）</summary>
        [DisplayName("異動摘要")]
        [Comment("異動摘要（建立：稽核員 / 實驗室 / 期間 / 原因；修改：變更前後；撤銷：撤銷原因）")]
        public string Summary { get; set; } = string.Empty;
    }

    public class LabAuditAssignmentHistoryConfiguration : IEntityTypeConfiguration<LabAuditAssignmentHistory>
    {
        public void Configure(EntityTypeBuilder<LabAuditAssignmentHistory> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("LabAuditAssignmentHistories", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Action).HasMaxLength(20).IsRequired();
            builder.Property(x => x.ActorName).HasMaxLength(256);
            builder.Property(x => x.Summary).HasMaxLength(1500).IsRequired();
            builder.HasIndex(x => new { x.AssignmentId, x.OccurredAt });
        }
    }
}
