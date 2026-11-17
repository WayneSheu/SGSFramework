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

        /// <summary>適用範圍：全域或實驗室專屬</summary>
        [DisplayName("適用範圍")]
        [Comment("適用範圍（Global=全域, Lab=實驗室專屬）")]
        public ReportCategoryScope Scope { get; set; } = ReportCategoryScope.Global;

        /// <summary>實驗室專屬類別之所屬區域實驗室；全域類別為 null</summary>
        [DisplayName("所屬實驗室")]
        [Comment("實驗室專屬類別之所屬區域實驗室（ORG Organization.TenantLabId）；全域類別為 null")]
        public Guid? OwnerTenantLabId { get; set; }

        // ===== 儲存位置 =====

        /// <summary>正式報告存放的檔案管理儲存分類（決定地端 / 雲端與容器）；null 使用模組預設 Report</summary>
        [DisplayName("儲存分類")]
        [Comment("正式報告存放的檔案管理儲存分類（FileManagement:Categories，決定地端 / 雲端與容器）；null 使用預設 Report 分類")]
        public string? StorageCategory { get; set; }

        /// <summary>儲存資料夾（儲存鍵前綴）；null 使用 reports/{類別代碼}</summary>
        [DisplayName("儲存資料夾")]
        [Comment("儲存資料夾（儲存鍵前綴，英數 / 底線 / 連字號以 / 分隔）；null 使用 reports/{類別代碼}")]
        public string? StorageFolder { get; set; }

        // ===== 資安管控 =====

        /// <summary>正式報告落地加密（AES-256-GCM）；啟用時即使儲存分類未設定加密亦強制加密</summary>
        [DisplayName("落地加密")]
        [Comment("正式報告落地加密（AES-256-GCM 信封加密）；啟用時即使儲存分類未設定加密亦強制加密")]
        public bool EncryptAtRest { get; set; } = true;

        /// <summary>下載浮水印：下載正式報告時提供逐頁點陣化並繪入下載者 / 時間 / IP 的浮水印版本</summary>
        [DisplayName("下載浮水印")]
        [Comment("下載正式報告時提供點陣化浮水印版本（下載者 / 時間 / IP），原檔不外流")]
        public bool WatermarkOnDownload { get; set; }

        /// <summary>防竄改驗證：簽署時產生公開查驗碼、下載時比對 SHA-256，並開放公開真偽查驗</summary>
        [DisplayName("防竄改驗證")]
        [Comment("防竄改驗證：簽署時產生公開查驗碼、下載時比對 SHA-256 並開放公開真偽查驗")]
        public bool TamperVerification { get; set; } = true;

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
            // 既有類別皆為全域、維持原有安全行為（加密 + 防竄改）
            builder.Property(x => x.Scope).HasMaxLength(10).HasDefaultValue(ReportCategoryScope.Global).HasSentinel(ReportCategoryScope.Global);
            builder.Property(x => x.StorageCategory).HasMaxLength(64);
            builder.Property(x => x.StorageFolder).HasMaxLength(200);
            builder.Property(x => x.EncryptAtRest).HasDefaultValue(true).HasSentinel(true);
            builder.Property(x => x.TamperVerification).HasDefaultValue(true).HasSentinel(true);
            builder.HasIndex(x => x.OwnerTenantLabId);
            builder.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired();
            builder.Property(x => x.UpdatedBy).HasMaxLength(256);
            builder.HasIndex(x => x.Code).IsUnique();
        }
    }
}
