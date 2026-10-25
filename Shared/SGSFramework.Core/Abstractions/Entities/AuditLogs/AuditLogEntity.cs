using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SGSFramework.Core.Abstractions.Entities.Base;
using SGSFramework.Core.Abstractions.Entities.Ledgers;
using System.ComponentModel;

namespace SGSFramework.Core.Abstractions.Entities.AuditLogs
{
    /// <summary>
    /// 審計日誌實體 (支援 SQL Server Ledger 防篡改鏈路與自癒機制)
    /// 使用 JSON 欄位來儲存變化，以便於後續的 NoSQL 存儲或靈活查詢。
    /// </summary>
    [Comment("資料稽核日誌實體")]
    public class AuditLogEntity : IEntity<long>, ILedgerEntity
    {
        [DisplayName("主鍵")]
        [Comment("稽核日誌識別主鍵")]
        public long Id { get; set; }

        /// <summary>
        /// 全鏈路追蹤 ID (對應 W3C TraceId 格式 32 位元 HEX)
        /// </summary>
        [DisplayName("全鏈路追蹤 ID")]
        [Comment("全鏈路追蹤 ID (對應 W3C TraceId 格式 32 位元 HEX)")]
        public string TraceId { get; set; } = string.Empty;

        [DisplayName("使用者識別")]
        [Comment("使用者識別")]
        public string? UserId { get; set; }

        [DisplayName("遠端 IP")]
        [Comment("遠端 IP 位址")]
        public string? RemoteIp { get; set; }

        [DisplayName("建立時間")]
        [Comment("建立時間")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        [DisplayName("時間戳記")]
        [Comment("時間戳記")]
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
        [DisplayName("資料表綱要")]
        [Comment("資料表綱要")]
        public string? Schema { get; set; }
        [DisplayName("資料表名稱")]
        [Comment("資料表名稱")]
        public string TableName { get; set; } = string.Empty;
        [DisplayName("操作類型")]
        [Comment("操作類型")]
        public string Action { get; set; } = string.Empty;
        [DisplayName("主鍵值")]
        [Comment("主鍵值")]
        public string? KeyValues { get; set; }
        [DisplayName("舊值")]
        [Comment("舊值")]
        public string? OldValues { get; set; }
        [DisplayName("新值")]
        [Comment("新值")]
        public string? NewValues { get; set; }
        [DisplayName("變更欄位")]
        [Comment("變更欄位")]
        public string? ChangedColumns { get; set; }

        // 防篡改鏈路欄位
        [DisplayName("前一筆雜湊值")]
        [Comment("前一筆雜湊值")]
        public string PreviousHash { get; set; } = string.Empty;
        [DisplayName("儲存雜湊值")]
        [Comment("儲存雜湊值")]
        public string StoredHash { get; set; } = string.Empty;

        // 自癒相關欄位
        [DisplayName("是否已修復")]
        [Comment("是否已修復")]
        public bool IsRepaired { get; set; }
        [DisplayName("修復時間")]
        [Comment("修復時間")]
        public DateTimeOffset? RepairedAt { get; set; }
        [DisplayName("差異原因")]
        [Comment("差異原因")] 
        public string? GapReason { get; set; }
        [DisplayName("原始儲存雜湊值")]
        [Comment("原始儲存雜湊值")]
        public string? OriginalStoredHash { get; set; }
    }



    public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogEntity>
    {
        public void Configure(EntityTypeBuilder<AuditLogEntity> builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // 僅指定 TableName，不寫死 Schema，使其能動態套用當前 DbContext 的 DefaultSchema
            builder.ToTable("AuditLogs");

            builder.HasKey(e => new { e.Id, e.CreatedAt });

        builder.Property(e => e.Id)
               .ValueGeneratedOnAdd();

        // 將 TraceId 放寬至 NVARCHAR(64) 或長度相容，防止長度溢位導致 SaveChanges 異常
        builder.Property(e => e.TraceId)
               .IsRequired()
               .HasMaxLength(64)
               .IsUnicode(false);

        builder.Property(e => e.UserId)
               .HasMaxLength(128)
               .IsRequired(false);

        builder.Property(e => e.RemoteIp)
               .HasMaxLength(64)
               .IsRequired(false);

        builder.Property(e => e.CreatedAt)
               .HasColumnType("datetimeoffset(7)")
               .HasDefaultValueSql("SYSDATETIMEOFFSET()")
               .IsRequired();

        builder.Property(e => e.Timestamp)
               .HasColumnType("datetimeoffset(7)")
               .HasDefaultValueSql("SYSDATETIMEOFFSET()")
               .IsRequired();

        builder.Property(e => e.Schema)
               .HasMaxLength(64)
               .IsRequired(false);

        builder.Property(e => e.TableName)
               .HasMaxLength(128)
               .IsRequired();

        builder.Property(e => e.Action)
               .HasMaxLength(50)
               .IsRequired();

        builder.Property(e => e.KeyValues).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(e => e.OldValues).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(e => e.NewValues).HasColumnType("nvarchar(max)").IsRequired(false);
        builder.Property(e => e.ChangedColumns).HasColumnType("nvarchar(max)").IsRequired(false);

        builder.Property(e => e.PreviousHash)
               .HasMaxLength(128)
               .IsRequired();

        builder.Property(e => e.StoredHash)
               .HasMaxLength(128)
               .IsRequired();

        builder.Property(e => e.IsRepaired)
               .HasDefaultValue(false)
               .IsRequired();

        builder.Property(e => e.RepairedAt)
               .HasColumnType("datetimeoffset(7)")
               .IsRequired(false);

        builder.Property(e => e.GapReason)
               .HasMaxLength(500)
               .IsRequired(false);

        builder.Property(e => e.OriginalStoredHash)
               .HasMaxLength(128)
               .IsRequired(false);

        builder.HasIndex(e => e.TraceId)
               .HasDatabaseName("IX_AuditLog_TraceId_Covering")
               .IncludeProperties(e => new { e.Action, e.CreatedAt, e.TableName });

        builder.HasIndex(e => e.IsRepaired)
               .HasDatabaseName("IX_AuditLog_IsRepaired")
               .HasFilter("[IsRepaired] = 0")
               .IncludeProperties(e => new { e.TableName, e.TraceId, e.GapReason });
    }
    }

}
