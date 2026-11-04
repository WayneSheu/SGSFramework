using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Entities.AuditLogs;
using SGSFramework.Core.FileManagement;
using System;
using System.ComponentModel;

namespace SGS.Modules.FMS.Infrastructure.Entities
{
    /// <summary>
    /// 檔案中繼資料：業務屬性（原始檔名、分類、關聯、建立者）與實體儲存屬性（儲存目標、容器、儲存鍵）完全分離。
    /// 實體檔名一律為 Guid + 安全副檔名，原始檔名僅供顯示與下載檔名使用（防路徑遍歷）。
    /// 刪除為虛擬刪除（IsDeleted），實體檔案由排程於保留期滿後批次清除（PurgedAtUtc）。
    /// </summary>
    [DisplayName("檔案中繼資料")]
    [Comment("檔案中繼資料：業務屬性與實體儲存屬性分離，刪除為虛擬刪除，實體檔案由排程批次清除")]
    public class FileMetadata : IAuditable
    {
        [DisplayName("識別主鍵")]
        [Comment("識別主鍵（同時作為實體檔名）")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [DisplayName("原始檔名")]
        [Comment("使用者上傳時的原始檔名（僅供顯示與下載，不作為儲存路徑）")]
        public string OriginalFileName { get; set; } = string.Empty;

        [DisplayName("副檔名")]
        [Comment("經檔案簽章 (Magic Number) 驗證後的安全副檔名（小寫，含句點）")]
        public string Extension { get; set; } = string.Empty;

        [DisplayName("內容類型")]
        [Comment("依檔案簽章判定的 MIME 類型（不採信用戶端 Content-Type）")]
        public string ContentType { get; set; } = string.Empty;

        [DisplayName("檔案大小")]
        [Comment("實際儲存的檔案大小（位元組，圖片為重新編碼後大小）")]
        public long FileSize { get; set; }

        [DisplayName("SHA-256")]
        [Comment("實際儲存內容的 SHA-256（十六進位小寫），作為 ETag 與完整性查驗")]
        public string Sha256 { get; set; } = string.Empty;

        [DisplayName("檔案分類")]
        [Comment("業務分類（對應 FileManagement:Categories，決定儲存目標與允許格式）")]
        public string Category { get; set; } = string.Empty;

        [DisplayName("儲存目標")]
        [Comment("儲存目標：LocalPrivate（地端機密）/ CloudPublic（公開資源）")]
        public StorageTarget StorageTarget { get; set; }

        [DisplayName("容器名稱")]
        [Comment("儲存容器（檔案系統為子目錄，物件儲存為 Bucket / Container）")]
        public string ContainerName { get; set; } = string.Empty;

        [DisplayName("儲存鍵")]
        [Comment("容器內的實體儲存鍵（yyyy/MM/{Id}{副檔名}），由系統產生")]
        public string StoredKey { get; set; } = string.Empty;

        [DisplayName("關聯類型")]
        [Comment("關聯的業務實體類型（例如 Report）；未關聯為 null")]
        public string? ReferenceType { get; set; }

        [DisplayName("關聯識別碼")]
        [Comment("關聯的業務實體識別碼；未關聯為 null")]
        public string? ReferenceId { get; set; }

        [DisplayName("圖片寬度")]
        [Comment("圖片寬度（像素）；非圖片為 null")]
        public int? ImageWidth { get; set; }

        [DisplayName("圖片高度")]
        [Comment("圖片高度（像素）；非圖片為 null")]
        public int? ImageHeight { get; set; }

        [DisplayName("是否刪除")]
        [Comment("虛擬刪除旗標；實體檔案於保留期滿後由排程清除")]
        public bool IsDeleted { get; set; }

        [DisplayName("刪除人員")]
        [Comment("執行刪除的人員")]
        public string? DeletedBy { get; set; }

        [DisplayName("刪除時間")]
        [Comment("虛擬刪除時間")]
        public DateTimeOffset? DeletedAtUtc { get; set; }

        [DisplayName("實體清除時間")]
        [Comment("實體檔案自儲存體清除的時間；null 表示實體檔案仍存在")]
        public DateTimeOffset? PurgedAtUtc { get; set; }

        [DisplayName("建立人員")]
        [Comment("上傳人員")]
        public string CreatedBy { get; set; } = string.Empty;

        [DisplayName("建立時間")]
        [Comment("上傳時間")]
        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

        [DisplayName("最後異動人員")]
        [Comment("最後異動人員")]
        public string? UpdatedBy { get; set; }

        [DisplayName("最後異動時間")]
        [Comment("最後異動時間")]
        public DateTimeOffset? UpdatedAtUtc { get; set; }
    }

    public class FileMetadataConfiguration : IEntityTypeConfiguration<FileMetadata>
    {
        public void Configure(EntityTypeBuilder<FileMetadata> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ToTable("FileMetadatas", "fms");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
            builder.Property(x => x.Extension).HasMaxLength(20).IsRequired();
            builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
            builder.Property(x => x.Category).HasMaxLength(100).IsRequired();
            builder.Property(x => x.StorageTarget).HasMaxLength(50).IsRequired();
            builder.Property(x => x.ContainerName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.StoredKey).HasMaxLength(500).IsRequired();
            builder.Property(x => x.ReferenceType).HasMaxLength(100);
            builder.Property(x => x.ReferenceId).HasMaxLength(100);
            builder.Property(x => x.DeletedBy).HasMaxLength(256);
            builder.Property(x => x.CreatedBy).HasMaxLength(256).IsRequired();
            builder.Property(x => x.UpdatedBy).HasMaxLength(256);

            builder.HasIndex(x => new { x.StorageTarget, x.ContainerName, x.StoredKey }).IsUnique();
            builder.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
            builder.HasIndex(x => new { x.Category, x.CreatedAtUtc });

            // 實體清除排程：已刪除且尚未清除者
            builder.HasIndex(x => new { x.IsDeleted, x.PurgedAtUtc, x.DeletedAtUtc });
        }
    }
}
