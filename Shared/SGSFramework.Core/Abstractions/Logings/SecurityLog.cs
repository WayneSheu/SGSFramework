using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Attributes;
using SGSFramework.Core.Abstractions.Entities.Base;
using SGSFramework.Core.Abstractions.Entities.Ledgers;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SGSFramework.Core.Abstractions.Logings
{

    /// <summary>
    /// 安全性審計總帳實體模型 (對應 MSSQL 2025 Ledger 防篡改唯讀資料表)
    /// </summary>
    [DisplayName("安全性審計總帳")]
    [Comment("安全性審計總帳實體模型 (對應 MSSQL 2025 Ledger 防篡改唯讀資料表)")]
    public class SecurityLog:ILedgerEntity
    {
        /// <summary>
        /// 流水號主鍵
        /// </summary>
        [DisplayName("流水號主鍵")]
        [Comment("流水號主鍵")]
        public int Id { get; set; }

        [AutoIndex]
        [Display(Name = "關聯識別碼")]
        [Comment("關聯識別碼")]
        public string? CorrelationId { get; set; }

        /// <summary>
        /// 日誌訊息主體
        /// </summary>
        [DisplayName("日誌訊息")]
        [Comment("日誌訊息")]
        public string? Message { get; set; }

        /// <summary>
        /// 日誌層級 (例如: Information, Warning, Error)
        /// </summary>
        [DisplayName("日誌層級")]
        [Comment("日誌層級 (例如: Information, Warning, Error)")] 
        public string? Level { get; set; }

        /// <summary>
        /// 高精度時間戳記 (帶時區)
        [DisplayName("時間戳記")]
        [Comment("高精度時間戳記 (帶時區)")]  
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }

        /// <summary>
        /// 異常堆疊追蹤資訊
        /// </summary>
        [DisplayName("異常堆疊追蹤資訊")]
        [Comment("異常堆疊追蹤資訊")]   
        public string? Exception { get; set; }

        /// <summary>
        /// 隨附結構化快照內容 (JSON 格式)
        /// </summary>
        [DisplayName("隨附結構化快照內容")]
        [Comment("隨附結構化快照內容 (JSON 格式)")]    
        public string? Properties { get; set; }

        /// <summary>
        /// 日誌分流類型 (例如: Security)
        /// </summary>
        [DisplayName("日誌分流類型")]
        [Comment("日誌分流類型 (例如: Security)")]
        public string? LogType { get; set; }

        /// <summary>
        /// 稽核事件分類 (例如: Auth.TokenRefresh)
        /// </summary>
        [DisplayName("稽核事件分類")]
        [Comment("稽核事件分類 (例如: Auth.TokenRefresh)")]
        public string? EventCategory { get; set; }

        /// <summary>
        /// 觸發事件的用戶識別碼
        /// </summary>
        [DisplayName("用戶識別碼")]
        [Comment("觸發事件的用戶識別碼")]
        public string? UserId { get; set; }

        /// <summary>
        /// 客戶端來源 IP 位址
        /// </summary>
        [DisplayName("客戶端 IP")]
        [Comment("客戶端來源 IP 位址")]
        public string? ClientIp { get; set; }

        //告警用欄位
        [DisplayName("告警識別碼")]
        [Comment("告警識別碼")]
        public string? AlertId { get; set; }
        
        [DisplayName("特徵碼")]
        [Comment("特徵碼")]
        public string? Fingerprint { get; set; }
        
        ///// <summary>
        ///// 對應 MSSQL 內建的 ledger_start_transaction_id
        ///// </summary>
        //public long LedgerStartTransactionId { get; set; }

        ///// <summary>
        ///// 對應 MSSQL 內建的 ledger_start_sequence_number
        ///// </summary>
        //public long LedgerStartSequenceNumber { get; set; }


    }


    /// <summary>
    /// 安全性審計總帳實體之 Fluent API 映射配置
    /// </summary>
    /// <summary>
    /// 安全性審計總帳實體之 Fluent API 映射配置
    /// </summary>
    public class SecurityAuditLedgerConfiguration : IEntityTypeConfiguration<SecurityLog>
    {
        public void Configure(EntityTypeBuilder<SecurityLog> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // 因 EF Core 不原生支援 .IsLedger() 語法，
            // 且實體表已透過手動 DDL 建立，此處僅需進行標準資料表名稱映射。
            builder.ToTable("SecurityLog");
            //啟用 Ledger Append-Only 屬性
            builder.HasAnnotation("SqlServer:IsLedger", true);
            // 鎖定僅限附加模式 (Append-Only)
            builder.HasAnnotation("SqlServer:IsAppendOnly", true);

            //主鍵與流水號配置
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id)
                   .UseIdentityColumn();

            builder.Property(e => e.CorrelationId).HasMaxLength(50).IsUnicode(false);

            //欄位屬性與實體資料庫類型 (DDL) 嚴格對齊
            builder.Property(e => e.Message)
                   .HasColumnType("NVARCHAR(MAX)")
                   .IsRequired(false);

            builder.Property(e => e.Level)
                   .HasMaxLength(128)
                   .HasColumnType("NVARCHAR(128)")
                   .IsRequired(false);

            builder.Property(e => e.Timestamp)
                   .HasColumnType("DATETIMEOFFSET(7)")
                   .HasDefaultValueSql("SYSDATETIMEOFFSET() AT TIME ZONE 'Taipei Standard Time'")
                   .IsRequired();

            builder.Property(e => e.Exception)
                   .HasColumnType("NVARCHAR(MAX)")
                   .IsRequired(false);

            builder.Property(e => e.Properties)
                   .HasColumnType("NVARCHAR(MAX)")
                   .IsRequired(false);

            builder.Property(e => e.LogType)
                   .HasMaxLength(64)
                   .HasColumnType("NVARCHAR(64)")
                   .IsRequired(false);

            builder.Property(e => e.EventCategory)
                   .HasMaxLength(128)
                   .HasColumnType("NVARCHAR(128)")
                   .IsRequired(false);

            builder.Property(e => e.UserId)
                   .HasMaxLength(128)
                   .HasColumnType("NVARCHAR(128)")
                   .IsRequired(false);

            builder.Property(e => e.ClientIp)
                   .HasMaxLength(64)
                   .HasColumnType("NVARCHAR(64)")
                   .IsRequired(false);

            // 告警欄位配置
            builder.Property(e => e.AlertId)
                .HasColumnType("char(32)")
                .IsRequired(false);

            builder.Property(e => e.Fingerprint)
                .HasColumnType("char(64)")
                .IsRequired(false);

            // 宣告啟用 MSSQL 2025 Ledger Append-Only 特性
            builder.HasAnnotation("SqlServer:IsLedgerAppendOnly", true);

        }
    }



}
