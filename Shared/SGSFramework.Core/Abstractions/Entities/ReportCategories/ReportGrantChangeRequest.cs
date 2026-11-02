#nullable enable

namespace SGSFramework.Core.Abstractions.Entities.ReportCategories
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using SGSFramework.Core.Abstractions.Entities.AuditLogs;
    using SGSFramework.Core.Abstractions.Permissions.Entities;
    using System;
    using System.ComponentModel;

    /// <summary>
    /// 報告授權變更申請類型
    /// </summary>
    public enum ReportGrantChangeType
    {
        /// <summary>新增報告類別授權（敏感授權：簽署、作廢、特檢密級或全域範圍）</summary>
        CreateGrant = 0,

        /// <summary>新增簽署人資格</summary>
        CreateQualification = 1
    }

    /// <summary>
    /// 報告授權變更申請（Maker-Checker）：敏感的類別授權與所有簽署人資格須經第二人核准方生效。
    /// 撤銷屬權限降低，立即生效，不經此流程。
    /// </summary>
    [DisplayName("報告授權變更申請")]
    [Comment("報告授權變更申請（Maker-Checker）：敏感的類別授權與所有簽署人資格須經第二人核准方生效")]
    public class ReportGrantChangeRequest : IAuditable
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [DisplayName("申請類型")]
        [Comment("申請類型（CreateGrant=新增類別授權, CreateQualification=新增簽署人資格）")]
        public ReportGrantChangeType RequestType { get; set; }

        /// <summary>申請內容（新增授權或資格之完整請求 JSON），核准時據以套用</summary>
        [DisplayName("申請內容")]
        [Comment("申請內容（新增授權或資格之完整請求 JSON），核准時據以套用")]
        public string PayloadJson { get; set; } = string.Empty;

        [DisplayName("申請摘要")]
        [Comment("申請摘要（顯示用）")]
        public string Summary { get; set; } = string.Empty;

        /// <summary>受授權之使用者（授權對象為使用者或簽署人資格時），核准者不得為此人</summary>
        [DisplayName("受授權使用者")]
        [Comment("受授權之使用者（授權對象為使用者或簽署人資格時），核准者不得為此人")]
        public Guid? SubjectUserId { get; set; }

        [DisplayName("申請原因")]
        [Comment("申請原因")]
        public string? RequestReason { get; set; }

        [DisplayName("申請狀態")]
        [Comment("申請狀態（Pending=待審核, Approved=核准, Rejected=駁回, Cancelled=撤回）")]
        public RoleChangeRequestStatus Status { get; set; } = RoleChangeRequestStatus.Pending;

        [DisplayName("申請人識別鍵")]
        [Comment("申請人識別鍵")]
        public Guid RequestedBy { get; set; }

        [DisplayName("申請人名稱")]
        [Comment("申請人名稱")]
        public string RequestedByName { get; set; } = string.Empty;

        [DisplayName("申請時間")]
        [Comment("申請時間")]
        public DateTimeOffset RequestedAt { get; set; }

        [DisplayName("審核人識別鍵")]
        [Comment("審核人識別鍵")]
        public Guid? ReviewedBy { get; set; }

        [DisplayName("審核人名稱")]
        [Comment("審核人名稱")]
        public string? ReviewedByName { get; set; }

        [DisplayName("審核時間")]
        [Comment("審核時間")]
        public DateTimeOffset? ReviewedAt { get; set; }

        [DisplayName("審核意見")]
        [Comment("審核意見")]
        public string? ReviewComment { get; set; }

        /// <summary>核准後建立之授權或資格識別鍵</summary>
        [DisplayName("套用結果識別鍵")]
        [Comment("核准後建立之授權或資格識別鍵")]
        public Guid? AppliedEntityId { get; set; }

        [DisplayName("建立時間")]
        [Comment("建立時間")]
        public DateTimeOffset CreatedAtUtc { get; set; }

        [DisplayName("建立人員")]
        [Comment("建立人員")]
        public string CreatedBy { get; set; } = string.Empty;

        [DisplayName("最後更新時間")]
        [Comment("最後更新時間")]
        public DateTimeOffset? UpdatedAtUtc { get; set; }

        [DisplayName("最後更新人員")]
        [Comment("最後更新人員")]
        public string? UpdatedBy { get; set; }
    }

    public class ReportGrantChangeRequestConfiguration : IEntityTypeConfiguration<ReportGrantChangeRequest>
    {
        public void Configure(EntityTypeBuilder<ReportGrantChangeRequest> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("ReportGrantChangeRequests", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.RequestType).HasMaxLength(30);
            builder.Property(x => x.Status).HasMaxLength(20);
            builder.Property(x => x.PayloadJson).IsRequired();
            builder.Property(x => x.Summary).HasMaxLength(500).IsRequired();
            builder.Property(x => x.RequestReason).HasMaxLength(1000);
            builder.Property(x => x.RequestedByName).HasMaxLength(256).IsRequired();
            builder.Property(x => x.ReviewedByName).HasMaxLength(256);
            builder.Property(x => x.ReviewComment).HasMaxLength(1000);
            builder.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired();
            builder.Property(x => x.UpdatedBy).HasMaxLength(256);
            builder.HasIndex(x => new { x.Status, x.RequestedAt });
        }
    }
}
