using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGSFramework.Core.Abstractions.Entities.Identities
{
    /// <summary>
    /// 儲存使用者裝置安全 Session 與長效憑證的資料表實體 (相容性與資安一體化規格)
    /// </summary>
    [Table("UserRefreshTokens")]
    public sealed class UserRefreshToken
    {
        [Key]
        public long Id { get; set; }

        /// <summary>
        /// 使用者唯一識別碼
        /// </summary>
        [Required]
        [StringLength(100)]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 裝置唯一指紋 (由前端 Vue / Blazor 經由 JS 產生的不重複雜湊值)
        /// </summary>
        [Required]
        [StringLength(255)]
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// 裝置名稱或瀏覽器特徵描述 (例如: Chrome on Windows)
        /// </summary>
        [Required]
        [StringLength(200)]
        public string DeviceName { get; set; } = string.Empty;

        /// <summary>
        /// 當前有效的 Refresh Token 雜湊值 (儲存 SHA256 避免明文外洩)
        /// </summary>
        [Required]
        [StringLength(64)]
        [Column("RefreshTokenHash")]
        public string RefreshTokenHash { get; set; } = string.Empty;

        /// <summary>
        /// 🚀 舊版本代碼相容性橋接屬性 (不映射至資料庫，直接與 RefreshTokenHash 雙向同步)
        /// </summary>
        [NotMapped]
        public string TokenHash
        {
            get => RefreshTokenHash;
            set => RefreshTokenHash = value;
        }

        /// <summary>
        /// 🚀 工作階段初次建立或重複登入覆蓋時的時間戳記
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 記錄上一次成功輪換的時間（用於寬限期計算）
        /// </summary>
        public DateTime? RotatedAt { get; set; }

        /// <summary>
        /// 快取上一次生成的全新 TokenHash，供寬限期內的併發請求直接複用
        /// </summary>
        [StringLength(64)]
        public string? ReusedTokenCache { get; set; }

        /// <summary>
        /// 憑證絕對過期時間
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// 該裝置最後一次發起請求或換票的活動時間 (用於 LIFO 被動擠出策略排序)
        /// </summary>
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 登入時的來源 IP 紀錄
        /// </summary>
        [Required]
        [StringLength(50)]
        public string ClientIp { get; set; } = string.Empty;

        /// <summary>
        /// 核心資安防禦熔斷標記：標記該憑證是否已被重放攻擊戳死、或已被主動註銷失效
        /// </summary>
        public bool IsDead { get; set; } = false;

        /// <summary>
        /// 標記該用戶所有/特定 Session 是否被安全鎖定
        /// </summary>
        public bool IsFrozen { get; set; }

        /// <summary>
        /// 記錄風險觸發原因或主動登出/熔斷稽核事由
        /// </summary>
        public string? RiskReason { get; set; }

        /// <summary>
        /// 補償擴充屬性：對齊 Controller 換票分流機制所需返還之明文憑證（不映射至資料庫欄位）
        /// </summary>
        [NotMapped]
        public string RawRefreshToken { get; set; } = string.Empty;

        /// <summary>
        /// 補償擴充屬性：對齊 Controller 換票分流機制所需返還之當前有效 AccessToken（不映射至資料庫欄位）
        /// </summary>
        [NotMapped]
        public string AccessToken { get; set; } = string.Empty;
    }

    public sealed class UserRefreshTokenConfiguration : IEntityTypeConfiguration<UserRefreshToken>
    {
        public void Configure(EntityTypeBuilder<UserRefreshToken> builder)
        {
            builder.ToTable("UserRefreshTokens");
            builder.HasKey(e => e.Id);

            builder.HasIndex(e => new { e.UserId, e.DeviceId })
                   .IsUnique()
                   .HasDatabaseName("UX_UserRefreshTokens_UserId_DeviceId");

            builder.HasIndex(e => new { e.UserId, e.DeviceId, e.IsDead })
                   .HasDatabaseName("IX_UserRefreshTokens_UserId_DeviceId_IsDead");

            builder.HasIndex(e => e.RefreshTokenHash)
                   .HasDatabaseName("IX_UserRefreshTokens_RefreshTokenHash");

            builder.Property(e => e.UserId)
                   .HasMaxLength(100)
                   .IsRequired();

            builder.Property(e => e.DeviceId)
                   .HasMaxLength(255)
                   .IsRequired();

            builder.Property(e => e.DeviceName)
                   .HasMaxLength(200)
                   .IsRequired();

            builder.Property(e => e.RefreshTokenHash)
                   .HasColumnName("RefreshTokenHash")
                   .HasMaxLength(64)
                   .IsRequired()
                   .IsFixedLength();

            builder.Property(e => e.ClientIp)
                   .HasMaxLength(50)
                   .IsRequired();

            builder.Property(e => e.CreatedAt)
                   .HasColumnType("datetime2")
                   .HasDefaultValueSql("SYSUTCDATETIME()") // 🚀 支援 MSSQL 自動產生存生時間
                   .IsRequired();

            builder.Property(e => e.ExpiresAt)
                   .HasColumnType("datetime2");

            builder.Property(e => e.LastActiveAt)
                   .HasColumnType("datetime2");

            builder.Property(e => e.IsDead)
                   .HasDefaultValue(false)
                   .IsRequired();
        }
    }
}