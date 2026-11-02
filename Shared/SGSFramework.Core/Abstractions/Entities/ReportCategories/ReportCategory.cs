#nullable enable

namespace SGSFramework.Core.Abstractions.Entities.ReportCategories
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using SGSFramework.Core.Abstractions.Entities.AuditLogs;
    using SGSFramework.Core.Abstractions.Entities.Base;
    using System;
    using System.ComponentModel;

    /// <summary>
    /// 報告類別主檔（檢驗領域，例：食品 FOOD、電子 RoHS、材料 MAT、環境 ENV）。
    /// 類別為報告權限隔離的核心維度，與區域實驗室（分公司）正交。
    /// </summary>
    [DisplayName("報告類別")]
    [Comment("報告類別主檔（檢驗領域，例：食品 FOOD、電子 RoHS、材料 MAT、環境 ENV），為報告權限隔離的核心維度")]
    public class ReportCategory : IAuditable, IConcurrencyAware
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵")]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>類別代碼（大寫英數，唯一）</summary>
        [DisplayName("類別代碼")]
        [Comment("類別代碼（大寫英數，唯一）")]
        public string Code { get; set; } = string.Empty;

        [DisplayName("類別名稱")]
        [Comment("類別名稱")]
        public string Name { get; set; } = string.Empty;

        [DisplayName("說明")]
        [Comment("說明")]
        public string? Description { get; set; }

        /// <summary>新建報告預設密級</summary>
        [DisplayName("預設密級")]
        [Comment("新建報告預設密級（Normal=常規, Confidential=機密, Restricted=特檢）")]
        public ConfidentialityLevel DefaultConfidentiality { get; set; } = ConfidentialityLevel.Normal;

        /// <summary>適用法規 / 認證標籤（逗號分隔，例：TFDA,ISO17025）</summary>
        [DisplayName("適用法規標籤")]
        [Comment("適用法規 / 認證標籤（逗號分隔，例：TFDA,ISO17025）")]
        public string? RegulationTags { get; set; }

        [DisplayName("排序")]
        [Comment("排序")]
        public int DisplayOrder { get; set; }

        [DisplayName("是否啟用")]
        [Comment("是否啟用")]
        public bool IsActive { get; set; } = true;

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

        [DisplayName("並行版本")]
        [Comment("並行版本（樂觀鎖）")]
        public byte[] RowVersion { get; set; } = [];
    }

    public class ReportCategoryConfiguration : IEntityTypeConfiguration<ReportCategory>
    {
        public void Configure(EntityTypeBuilder<ReportCategory> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("ReportCategories", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Code).HasMaxLength(32).IsRequired();
            builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Description).HasMaxLength(500);
            builder.Property(x => x.RegulationTags).HasMaxLength(200);
            builder.Property(x => x.DefaultConfidentiality).HasMaxLength(20);
            builder.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired();
            builder.Property(x => x.UpdatedBy).HasMaxLength(256);
            builder.HasIndex(x => x.Code).IsUnique();
        }
    }
}
