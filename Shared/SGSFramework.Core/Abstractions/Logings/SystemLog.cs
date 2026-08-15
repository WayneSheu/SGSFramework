
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Ledgers;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGSFramework.Core.Abstractions.Logings
{
    public class SystemLog : IHashChainLog, ILedgerEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; } // bigint
        [AutoIndex]
        public DateTime TimeStamp { get; set; } // Serilog 預設使用 TimeStamp DateTimeOffset.Now
        public string? Message { get; set; }
        public string? Level { get; set; }
        public string? Exception { get; set; }

        // 自定義業務欄位
        [AutoIndex]
        public string? TenantId { get; set; }
        public string? UserId { get; set; }
        public string? ModuleName { get; set; }
        public string? Operation { get; set; }
        [AutoIndex]
        [Display(Name = "關聯識別碼")]
        public string? CorrelationId { get; set; }
        public string? IP { get; set; }
        public string? Url { get; set; }

        //[Required]
        public string? Payload { get; set; } = string.Empty;

        // 前一筆記錄的雜湊值 (串連關鍵)
        //[Required]
        [MaxLength(64)]
        public string? PrevHash { get; set; } = string.Empty;

        // 本筆記錄的雜湊值 (計算結果)
        //[Required]
        [MaxLength(64)]
        public string? CurrentHash { get; set; } = string.Empty;

        [Required]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        //告警
        public string? AlertId { get; set; }
        public string? Fingerprint { get; set; }


    }
    public class SystemLogConfiguration : IEntityTypeConfiguration<SystemLog>
    {
        public void Configure(EntityTypeBuilder<SystemLog> builder)
        {
            // 1. 指定資料表名稱
            builder.ToTable("SystemLogs");
            // 啟用密碼學分類帳
            builder.HasAnnotation("SqlServer:IsLedger", true);
            // 鎖定僅限附加模式 (Append-Only)
            builder.HasAnnotation("SqlServer:IsAppendOnly", true);


            // 2. 主鍵配置 (bigint IDbuilder)
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedOnAdd();

            // 3. Serilog 標準欄位配置
            builder.Property(e => e.Message).IsRequired(false);
            builder.Property(e => e.Level).HasMaxLength(128);
            builder.Property(e => e.TimeStamp).IsRequired();
            builder.Property(e => e.Exception).IsRequired(false);

            // 4. 自定義業務欄位配置 (長度與索引)
            builder.Property(e => e.CorrelationId).HasMaxLength(50).IsUnicode(false);
            builder.Property(e => e.TenantId).HasMaxLength(50);
            builder.Property(e => e.UserId).HasMaxLength(50);
            builder.Property(e => e.ModuleName).HasMaxLength(50);
            builder.Property(e => e.IP).HasMaxLength(45).IsUnicode(false);
            builder.Property(e => e.Url)
            .HasMaxLength(2083)
            .IsRequired(false) // 根據需求設定是否允許為 NULL
            .IsUnicode(true); // 使用 nvarchar

            // 5. 時間與預設值配置
            builder.Property(e => e.CreatedAt)
                .HasDefaultValueSql("SYSUTCDATETIME()") // SQL Server 2025 自動產生 UTC 時間
                .ValueGeneratedOnAdd();

            // 強型別欄位映射與長度約束
            builder.Property(e => e.AlertId)
                .HasColumnType("char(32)")
                .IsRequired(false);

            builder.Property(e => e.Fingerprint)
                .HasColumnType("char(64)")
                .IsRequired(false);

            // 6. 效能優化：建立索引
            //builder.HasIndex(e => e.TimeStamp);      // 時間查詢最頻繁
            //builder.HasIndex(e => e.TenantId);       // 多租戶過濾
            //builder.HasIndex(e => e.CorrelationId);  // 跨模組追蹤

            // 宣告啟用 MSSQL 2025 Ledger Append-Only 特性
            builder.HasAnnotation("SqlServer:IsLedgerAppendOnly", true);

        }
    }

}