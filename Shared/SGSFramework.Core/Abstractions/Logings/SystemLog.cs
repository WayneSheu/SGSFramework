
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Ledgers;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SGSFramework.Core.Abstractions.Logings
{
    [DisplayName("系統日誌")]
    [Comment("系統日誌實體：用於記錄系統運行狀態、事件、錯誤與稽核資訊")]
    public class SystemLog : IHashChainLog, ILedgerEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [DisplayName("識別鍵")]
        [Comment("識別鍵")]    
        public long Id { get; set; } // bigint
        
        [AutoIndex]
        [DisplayName("時間戳記")]
        [Comment("時間戳記")]
        public DateTimeOffset TimeStamp { get; set; }
        
        [DisplayName("訊息")]
        [Comment("訊息")]
        public string? Message { get; set; }

        [DisplayName("層級")]
        [Comment("層級")]
        public string? Level { get; set; }

        [DisplayName("異常")]
        [Comment("異常")]
        public string? Exception { get; set; }

        // 自定義業務欄位
        [AutoIndex]
        [DisplayName("區域實驗室關聯識別碼")]  
        [Comment("區域實驗室關聯識別碼")]  
        public string? TenantId { get; set; }
       
        [AutoIndex]
        [DisplayName("用戶識別碼")]
        [Comment("用戶識別碼")]
        public string? UserId { get; set; }
       
        [DisplayName("模組名稱")]
        [Comment("模組名稱")]
        public string? ModuleName { get; set; }
        
        [DisplayName("操作")]
        [Comment("操作")]
        public string? Operation { get; set; }
        
        [AutoIndex]
        [DisplayName("關聯識別碼")]
        [Comment("關聯識別碼")]
        public string? CorrelationId { get; set; }
        
        [DisplayName("IP位址")]
        [Comment("IP位址")]
        public string? IP { get; set; }
        
        [DisplayName("URL")]
        [Comment("URL")]
        public string? Url { get; set; }

        //[Required]
        [DisplayName("有效載荷")]
        [Comment("有效載荷")]
        public string? Payload { get; set; } = string.Empty;

        // 前一筆記錄的雜湊值 (串連關鍵)
        //[Required]
        [MaxLength(64)]
        [DisplayName("前一筆雜湊值")]
        [Comment("前一筆雜湊值")]
        public string? PrevHash { get; set; } = string.Empty;

        // 本筆記錄的雜湊值 (計算結果)
        //[Required]
        [MaxLength(64)]
        [DisplayName("雜湊值")]
        [Comment("雜湊值")]
        public string? CurrentHash { get; set; } = string.Empty;

        [Required]
        [DisplayName("建立時間")]
        [Comment("建立時間")]   
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        //告警
        [DisplayName("告警識別碼")]
        [Comment("告警識別碼")]  
        public string? AlertId { get; set; }

        [DisplayName("特徵碼")]
        [Comment("特徵碼")]
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
            builder.Property(e => e.TimeStamp)
                .HasColumnType("DATETIMEOFFSET(7)")
                .HasDefaultValueSql("SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'")
                .ValueGeneratedOnAdd();

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
                .HasColumnType("DATETIMEOFFSET(7)")
                .HasDefaultValueSql("SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'")
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