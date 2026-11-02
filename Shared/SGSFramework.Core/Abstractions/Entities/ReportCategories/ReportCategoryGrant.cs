#nullable enable

namespace SGSFramework.Core.Abstractions.Entities.ReportCategories
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Metadata.Builders;
    using SGSFramework.Core.Abstractions.Entities.AuditLogs;
    using System;
    using System.ComponentModel;

    /// <summary>
    /// 報告類別授權：授予使用者或角色於「類別 × 區域實驗室」下可執行的動作、資料範圍與可存取的最高密級。
    /// 使用者授權與其有效角色之授權取聯集。紀錄不刪除，以撤銷保留完整稽核軌跡。
    /// </summary>
    [DisplayName("報告類別授權")]
    [Comment("報告類別授權：授予使用者或角色於「類別 × 區域實驗室」下可執行的動作、資料範圍與可存取的最高密級。紀錄不刪除，以撤銷保留稽核軌跡")]
    public class ReportCategoryGrant : IAuditable
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [DisplayName("授權對象類型")]
        [Comment("授權對象類型（User=使用者, Role=角色）")]
        public GrantPrincipalType PrincipalType { get; set; }

        [DisplayName("授權對象識別鍵")]
        [Comment("授權對象識別鍵（使用者或角色 Id）")]
        public Guid PrincipalId { get; set; }

        [DisplayName("類別代碼")]
        [Comment("類別代碼（ReportCategory.Code）")]
        public string CategoryCode { get; set; } = string.Empty;

        /// <summary>限定區域實驗室；null 表示不限（須搭配 Global 範圍方可跨分公司）</summary>
        [DisplayName("限定區域實驗室")]
        [Comment("限定區域實驗室；null 表示不限實驗室")]
        public Guid? TenantLabId { get; set; }

        [DisplayName("授權動作")]
        [Comment("授權動作位元旗標（View=1, Edit=2, Review=4, Sign=8, Void=16, Download=32, ViewAudit=64, ViewBlindIdentity=128）")]
        public ReportActions Actions { get; set; }

        [DisplayName("資料範圍")]
        [Comment("資料範圍（Assigned=僅指派, Lab=所屬實驗室, Global=全域）")]
        public ReportDataScope DataScope { get; set; } = ReportDataScope.Assigned;

        [DisplayName("最高密級")]
        [Comment("可存取的最高密級（Normal=常規, Confidential=機密, Restricted=特檢）")]
        public ConfidentialityLevel MaxConfidentiality { get; set; } = ConfidentialityLevel.Normal;

        [DisplayName("生效起始")]
        [Comment("生效起始（含）")]
        public DateTimeOffset EffectiveFrom { get; set; }

        [DisplayName("生效迄止")]
        [Comment("生效迄止（含）；null 表示無期限")]
        public DateTimeOffset? EffectiveTo { get; set; }

        [DisplayName("授權原因")]
        [Comment("授權原因")]
        public string? Reason { get; set; }

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
        public bool IsActiveAt(DateTimeOffset now) =>
            RevokedAt is null && EffectiveFrom <= now && (EffectiveTo is null || now <= EffectiveTo.Value);
    }

    public class ReportCategoryGrantConfiguration : IEntityTypeConfiguration<ReportCategoryGrant>
    {
        public void Configure(EntityTypeBuilder<ReportCategoryGrant> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("ReportCategoryGrants", "core");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.CategoryCode).HasMaxLength(32).IsRequired();
            builder.Property(x => x.PrincipalType).HasMaxLength(10);
            // 位元旗標以整數保存（全域慣例為列舉轉字串，旗標組合不適用）
            builder.Property(x => x.Actions).HasConversion<int>();
            builder.Property(x => x.DataScope).HasMaxLength(20);
            builder.Property(x => x.MaxConfidentiality).HasMaxLength(20);
            builder.Property(x => x.Reason).HasMaxLength(500);
            builder.Property(x => x.RevokedByName).HasMaxLength(256);
            builder.Property(x => x.RevokeReason).HasMaxLength(500);
            builder.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired();
            builder.Property(x => x.UpdatedBy).HasMaxLength(256);
            builder.HasIndex(x => new { x.PrincipalType, x.PrincipalId });
            builder.HasIndex(x => new { x.CategoryCode, x.TenantLabId });
        }
    }
}
