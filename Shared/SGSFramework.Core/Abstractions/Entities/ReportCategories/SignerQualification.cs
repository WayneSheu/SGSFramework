#nullable enable

namespace SGSFramework.Core.Abstractions.Entities.ReportCategories
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using SGSFramework.Core.Abstractions.Entities.AuditLogs;
    using System;
    using System.ComponentModel;

    /// <summary>
    /// 報告簽署人資格：使用者於「類別 × 區域實驗室」之 TAF / 主管機關認可簽署資格與有效期間。
    /// 簽署動作除報告類別授權外，必須具備於簽署當下有效之資格。紀錄不刪除，以撤銷保留完整稽核軌跡。
    /// </summary>
    [DisplayName("報告簽署人資格")]
    [Comment("報告簽署人資格：使用者於「類別 × 區域實驗室」之認可簽署資格與有效期間。紀錄不刪除，以撤銷保留稽核軌跡")]
    public class SignerQualification : IAuditable
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [DisplayName("使用者識別鍵")]
        [Comment("使用者識別鍵")]
        public Guid UserId { get; set; }

        [DisplayName("類別代碼")]
        [Comment("類別代碼（ReportCategory.Code）")]
        public string CategoryCode { get; set; } = string.Empty;

        [DisplayName("區域實驗室")]
        [Comment("區域實驗室（ORG Organization.TenantLabId）")]
        public Guid TenantLabId { get; set; }

        /// <summary>認證機構核發之簽署人編號（例：TAF 報告簽署人編號）</summary>
        [DisplayName("資格證號")]
        [Comment("認證機構核發之簽署人編號（例：TAF 報告簽署人編號）")]
        public string CertificateNo { get; set; } = string.Empty;

        [DisplayName("資格生效起始")]
        [Comment("資格生效起始（含）")]
        public DateTimeOffset ValidFrom { get; set; }

        [DisplayName("資格生效迄止")]
        [Comment("資格生效迄止（含）")]
        public DateTimeOffset ValidTo { get; set; }

        [DisplayName("備註")]
        [Comment("備註")]
        public string? Remark { get; set; }

        [DisplayName("撤銷時間")]
        [Comment("撤銷時間")]
        public DateTimeOffset? RevokedAt { get; set; }

        [DisplayName("撤銷人員")]
        [Comment("撤銷人員")]
        public string? RevokedByName { get; set; }

        [DisplayName("撤銷原因")]
        [Comment("撤銷原因")]
        public string? RevokeReason { get; set; }

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

        /// <summary>指定時間點是否有效（未撤銷且於起訖期間內）</summary>
        public bool IsActiveAt(DateTimeOffset now) => RevokedAt is null && ValidFrom <= now && now <= ValidTo;
    }

    public class SignerQualificationConfiguration : IEntityTypeConfiguration<SignerQualification>
    {
        public void Configure(EntityTypeBuilder<SignerQualification> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("SignerQualifications", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.CategoryCode).HasMaxLength(32).IsRequired();
            builder.Property(x => x.CertificateNo).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Remark).HasMaxLength(500);
            builder.Property(x => x.RevokedByName).HasMaxLength(256);
            builder.Property(x => x.RevokeReason).HasMaxLength(500);
            builder.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired();
            builder.Property(x => x.UpdatedBy).HasMaxLength(256);
            builder.HasIndex(x => new { x.UserId, x.CategoryCode, x.TenantLabId });
        }
    }
}
