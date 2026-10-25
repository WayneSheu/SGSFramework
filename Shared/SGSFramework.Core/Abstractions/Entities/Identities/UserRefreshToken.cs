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
    [Table("UserRefreshTokens", Schema = "core")]
    [Display(Name = "使用者裝置安全 Session 與長效憑證")]
    public sealed class UserRefreshToken
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Display(Name = "識別鍵")]
        [Comment("識別鍵")]
        public long Id { get; set; }

        /// <summary>
        /// 使用者唯一識別碼
        /// </summary>
        [Required]
        [StringLength(100)]
        [Display(Name = "使用者唯一識別碼")]
        [Comment("使用者唯一識別碼")]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 裝置唯一指紋 (由前端 Vue / Blazor 經由 JS 產生的不重複雜湊值)
        /// </summary>
        [Required]
        [StringLength(255)]
        [Display(Name = "裝置唯一指紋")]
        [Comment("裝置唯一指紋 (由前端 Vue / Blazor 經由 JS 產生的不重複雜湊值)")]  
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// 裝置名稱或瀏覽器特徵描述 (例如: Chrome on Windows)
        /// </summary>
        [Required]
        [StringLength(200)]
        [Display(Name = "裝置名稱或瀏覽器特徵描述")]
        [Comment("裝置名稱或瀏覽器特徵描述 (例如: Chrome on Windows)")]
        public string DeviceName { get; set; } = string.Empty;

        /// <summary>
        /// 當前有效的 Refresh Token 雜湊值 (儲存 SHA256 避免明文外洩)
        /// </summary>
        [Required]
        [StringLength(64)]
        [Display(Name = "當前有效的 Refresh Token 雜湊值")]
        [Column("RefreshTokenHash")]
        public string RefreshTokenHash { get; set; } = string.Empty;

        /// <summary>
        /// 舊版本代碼相容性橋接屬性 (不映射至資料庫，直接與 RefreshTokenHash 雙向同步)
        /// </summary>
        [NotMapped]
        public string TokenHash
        {
            get => RefreshTokenHash;
            set => RefreshTokenHash = value;
        }

        /// <summary>
        /// 工作階段初次建立或重複登入覆蓋時的時間戳記
        /// </summary>
        [Display(Name = "建立時間")]
        [Comment("建立時間")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 記錄上一次成功輪換的時間（用於寬限期計算）
        /// </summary>
        [Display(Name = "上一次成功輪換的時間")]
        [Comment("上一次成功輪換的時間（用於寬限期計算）")]    
        public DateTime? RotatedAt { get; set; }

        /// <summary>
        /// 快取上一次生成的全新 TokenHash，供寬限期內的併發請求直接複用
        /// </summary>
        [StringLength(64)]
        [Display(Name = "快取上一次生成的全新 TokenHash")]
        [Comment("快取上一次生成的全新 TokenHash，供寬限期內的併發請求直接複用")]    
        public string? ReusedTokenCache { get; set; }

        /// <summary>
        /// 憑證絕對過期時間
        /// </summary>
        [Display(Name = "憑證絕對過期時間")]
        [Comment("憑證絕對過期時間")]
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// 該裝置最後一次發起請求或換票的活動時間 (用於 LIFO 被動擠出策略排序)
        /// </summary>
        [Display(Name = "該裝置最後一次發起請求或換票的活動時間")]
        [Comment("該裝置最後一次發起請求或換票的活動時間 (用於 LIFO 被動擠出策略排序)")]
        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 登入時的來源 IP 紀錄
        /// </summary>
        [Required]
        [StringLength(50)]
        [Display(Name = "登入時的來源 IP 紀錄")]
        [Comment("登入時的來源 IP 紀錄")]   
        public string ClientIp { get; set; } = string.Empty;

        /// <summary>
        /// 核心資安防禦熔斷標記：標記該憑證是否已被重放攻擊戳死、或已被主動註銷失效
        /// </summary>
        [Display(Name = "核心資安防禦熔斷標記")]
        [Comment("核心資安防禦熔斷標記：標記該憑證是否已被重放攻擊戳死、或已被主動註銷失效")]   
        public bool IsDead { get; set; } = false;

        /// <summary>
        /// 標記該用戶所有/特定 Session 是否被安全鎖定
        /// </summary>
        [Display(Name = "標記該用戶所有/特定 Session 是否被安全鎖定")]
        [Comment("標記該用戶所有/特定 Session 是否被安全鎖定")] 
        public bool IsFrozen { get; set; }

        /// <summary>
        /// 記錄風險觸發原因或主動登出/熔斷稽核事由
        /// </summary>
        [Display(Name = "風險觸發原因或主動登出/熔斷稽核事由")]
        [Comment("記錄風險觸發原因或主動登出/熔斷稽核事由")]
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
            builder.ToTable("UserRefreshTokens", "core");
            builder.HasKey(e => e.Id);

            // 高併發 Token 輪轉與熔斷覆蓋索引，包含所有狀態與條件欄位，杜絕 Bookmark Lookup
            builder.HasIndex(e => new { e.UserId, e.DeviceId, e.RefreshTokenHash, e.IsDead, e.IsFrozen })
                   .HasDatabaseName("IX_UserRefreshTokens_Lockdown_Rotation_Seek");

            // 保留唯一約束，用於初次建立或強制裝置限制時的精準檢索
            builder.HasIndex(e => new { e.UserId, e.DeviceId })
                   .IsUnique()
                   .HasDatabaseName("UX_UserRefreshTokens_UserId_DeviceId");

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
                   .IsFixedLength(); //SHA256 固定長度效能優勢

            builder.Property(e => e.ClientIp)
                   .HasMaxLength(50)
                   .IsRequired();

            builder.Property(e => e.CreatedAt)
                   .HasColumnType("datetime2")
                   .HasDefaultValueSql("SYSUTCDATETIME()")
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