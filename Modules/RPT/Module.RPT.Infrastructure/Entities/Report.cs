using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.Abstractions.Entities.Base;
using SGSFramework.Core.Abstractions.Entities.ReportCategories;

namespace SGS.Modules.RPT.Infrastructure.Entities;

/// <summary>
/// 檢驗報告：依類別（檢驗領域）與區域實驗室（分公司）隔離，生命週期 Draft → InReview → Reviewed → Published → Voided。
/// 發行後唯讀；修正須作廢並重發新版（SupersedesReportId 指向前一版）。報告不刪除。
/// </summary>
[DisplayName("檢驗報告")]
[Comment("檢驗報告：依類別與區域實驗室隔離，生命週期 Draft → InReview → Reviewed → Published → Voided；發行後唯讀，修正須作廢重發")]
public class Report : IAuditable, IConcurrencyAware
{
    [Comment("識別主鍵")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Comment("報告編號（唯一；重發版本加 -R{n}）")]
    public string ReportNo { get; set; } = string.Empty;

    [Comment("版本（首版 1，每次重發 +1）")]
    public int Version { get; set; } = 1;

    [Comment("取代之前一版報告識別鍵（重發時）")]
    public Guid? SupersedesReportId { get; set; }

    [Comment("報告類別代碼（core.ReportCategories.Code）")]
    public string CategoryCode { get; set; } = string.Empty;

    [Comment("所屬區域實驗室（分公司，ORG Organization.TenantLabId）")]
    public Guid TenantLabId { get; set; }

    [Comment("報告標題")]
    public string Title { get; set; } = string.Empty;

    [Comment("客戶代碼")]
    public string? CustomerCode { get; set; }

    /// <summary>客戶名稱（落地加密；盲樣時對檢驗人員遮蔽）</summary>
    [Encrypted]
    [Comment("客戶名稱（落地加密；盲樣時對檢驗人員遮蔽）")]
    public string? CustomerName { get; set; }

    [Comment("樣品編號（盲樣時檢驗人員僅能看到此欄）")]
    public string SampleNo { get; set; } = string.Empty;

    [Comment("樣品描述（盲樣時遮蔽）")]
    public string? SampleDescription { get; set; }

    [Comment("法規密級（Normal=常規, Confidential=機密, Restricted=特檢）")]
    public ConfidentialityLevel Confidentiality { get; set; }

    [Comment("是否為盲樣（隱藏客戶與來源資訊）")]
    public bool IsBlind { get; set; }

    [Comment("生命週期狀態（Draft, InReview, Reviewed, Published, Voided）")]
    public ReportLifecycleStatus Status { get; set; } = ReportLifecycleStatus.Draft;

    [Comment("作者（數據登錄者）識別鍵")]
    public Guid AuthorId { get; set; }

    [Comment("作者名稱")]
    public string AuthorName { get; set; } = string.Empty;

    [Comment("送審時間")]
    public DateTimeOffset? SubmittedAt { get; set; }

    [Comment("審核者識別鍵")]
    public Guid? ReviewerId { get; set; }

    [Comment("審核者名稱")]
    public string? ReviewerName { get; set; }

    [Comment("審核完成時間")]
    public DateTimeOffset? ReviewedAt { get; set; }

    [Comment("簽署人識別鍵")]
    public Guid? SignerId { get; set; }

    [Comment("簽署人名稱")]
    public string? SignerName { get; set; }

    [Comment("簽署人資格證號（簽署當下快照）")]
    public string? SignerCertificateNo { get; set; }

    [Comment("簽署 / 發行時間")]
    public DateTimeOffset? SignedAt { get; set; }

    [Comment("正式報告 PDF 於檔案管理模組 (fms.FileMetadatas) 的檔案識別碼")]
    public Guid? PdfFileId { get; set; }

    [Comment("舊版正式報告 PDF 儲存連結（LocalFileStorageHelper），僅供改接檔案管理前已簽署的報告讀取")]
    public string? PdfFileLink { get; set; }

    [Comment("正式報告 PDF 之 SHA-256 雜湊（小寫十六進位）")]
    public string? PdfSha256 { get; set; }

    [Comment("公開真偽查驗代碼")]
    public string? VerificationToken { get; set; }

    [Comment("作廢時間")]
    public DateTimeOffset? VoidedAt { get; set; }

    [Comment("作廢原因")]
    public string? VoidReason { get; set; }

    [Comment("建立時間")]
    public DateTimeOffset CreatedAtUtc { get; set; }

    [Comment("建立人員")]
    public string CreatedBy { get; set; } = string.Empty;

    [Comment("最後更新時間")]
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    [Comment("最後更新人員")]
    public string? UpdatedBy { get; set; }

    [Comment("並行版本（樂觀鎖）")]
    public byte[] RowVersion { get; set; } = [];

    public List<ReportResult> Results { get; set; } = [];

    public List<ReportAssignment> Assignments { get; set; } = [];
}

public sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Reports", "rpt");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReportNo).HasMaxLength(40).IsRequired();
        builder.Property(x => x.CategoryCode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CustomerCode).HasMaxLength(64);
        builder.Property(x => x.CustomerName).HasMaxLength(1000);
        builder.Property(x => x.SampleNo).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SampleDescription).HasMaxLength(500);
        builder.Property(x => x.Confidentiality).HasMaxLength(20);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.AuthorName).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ReviewerName).HasMaxLength(256);
        builder.Property(x => x.SignerName).HasMaxLength(256);
        builder.Property(x => x.SignerCertificateNo).HasMaxLength(64);
        builder.Property(x => x.PdfFileLink).HasMaxLength(500);
        builder.HasIndex(x => x.PdfFileId);
        builder.Property(x => x.PdfSha256).HasMaxLength(64);
        builder.Property(x => x.VerificationToken).HasMaxLength(32);
        builder.Property(x => x.VoidReason).HasMaxLength(500);
        builder.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired();
        builder.Property(x => x.UpdatedBy).HasMaxLength(256);

        builder.HasIndex(x => x.ReportNo).IsUnique();
        builder.HasIndex(x => x.VerificationToken).IsUnique().HasFilter("[VerificationToken] IS NOT NULL");
        builder.HasIndex(x => new { x.TenantLabId, x.CategoryCode, x.Status });
        builder.HasIndex(x => x.SampleNo);

        builder.HasMany(x => x.Results).WithOne().HasForeignKey(r => r.ReportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Assignments).WithOne().HasForeignKey(a => a.ReportId).OnDelete(DeleteBehavior.Restrict);
    }
}
