#nullable enable

namespace SGSFramework.Core.Abstractions.Entities.ReportCategories
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using System;
    using System.ComponentModel;

    /// <summary>
    /// 報告類別承作實驗室：指定哪些區域實驗室（分公司）可承作此類別報告。
    /// 未列入之分公司無法於該類別建立或簽署報告。
    /// </summary>
    [DisplayName("報告類別承作實驗室")]
    [Comment("報告類別承作實驗室：指定哪些區域實驗室（分公司）可承作此類別報告")]
    public class ReportCategoryLabAssignment
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [DisplayName("類別代碼")]
        [Comment("類別代碼（ReportCategory.Code）")]
        public string CategoryCode { get; set; } = string.Empty;

        [DisplayName("區域實驗室")]
        [Comment("區域實驗室（ORG Organization.TenantLabId）")]
        public Guid TenantLabId { get; set; }

        [DisplayName("建立時間")]
        [Comment("建立時間")]
        public DateTimeOffset CreatedAt { get; set; }

        [DisplayName("建立人員")]
        [Comment("建立人員")]
        public string? CreatedByName { get; set; }
    }

    public class ReportCategoryLabAssignmentConfiguration : IEntityTypeConfiguration<ReportCategoryLabAssignment>
    {
        public void Configure(EntityTypeBuilder<ReportCategoryLabAssignment> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("ReportCategoryLabAssignments", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.CategoryCode).HasMaxLength(32).IsRequired();
            builder.Property(x => x.CreatedByName).HasMaxLength(256);
            builder.HasIndex(x => new { x.CategoryCode, x.TenantLabId }).IsUnique();
            builder.HasIndex(x => x.TenantLabId);
        }
    }
}
